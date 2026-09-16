using System.Text;
using System.Text.RegularExpressions;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Locates the Blackthorn BTUI realtime relay credentials on disk. The launcher's
/// CEF session stores the full window URLs (including btuiRelayUrl and
/// btuiRelayToken) in the Chromium History file; the relay token also sits in
/// btui-mvp.registry. Everything needed is plain file bytes — no process access.
/// </summary>
public static class BlackthornRelayDiscovery
{
    private static readonly Regex RelayUrlPattern = new(
        @"btuiRelayUrl=([^&\s""']+)", RegexOptions.Compiled);
    private static readonly Regex RelayCommandUrlPattern = new(
        @"btuiRelayCommandUrl=([^&\s""']+)", RegexOptions.Compiled);
    private static readonly Regex RelayTokenPattern = new(
        @"btuiRelayToken=([0-9a-fA-F]{16,128})", RegexOptions.Compiled);
    private static readonly Regex RegistryTokenPattern = new(
        @"[0-9a-fA-F]{64}", RegexOptions.Compiled);

    public sealed record Endpoint(Uri EventsUrl, Uri CommandsUrl, string Token);

    /// <summary>Try to discover the live relay endpoint for the running session.</summary>
    public static Endpoint? TryDiscover(string? btuiCefRoot = null)
    {
        var root = btuiCefRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Blackthorn", "btui-cef");
        if (!Directory.Exists(root))
        {
            return null;
        }

        foreach (var sessionDir in SessionDirs(root))
        {
            var endpoint = ReadFromHistory(sessionDir) ?? ReadFromRegistry(sessionDir);
            if (endpoint is not null)
            {
                return endpoint;
            }
        }
        return null;
    }

    private static IEnumerable<string> SessionDirs(string root)
    {
        // Newest session first — stale sessions from earlier runs linger on disk.
        return Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Where(d => Path.GetFileName(Path.GetDirectoryName(d)!) == "sessions")
            .OrderByDescending(Directory.GetLastWriteTimeUtc);
    }

    private static Endpoint? ReadFromHistory(string sessionDir)
    {
        var history = Path.Combine(sessionDir, "cef-cache", "Default", "History");
        if (!File.Exists(history))
        {
            return null;
        }

        string text;
        try
        {
            using var stream = new FileStream(history, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            text = Encoding.Latin1.GetString(ms.ToArray());
        }
        catch (IOException)
        {
            return null;
        }

        var token = RelayTokenPattern.Match(text);
        var relay = RelayUrlPattern.Match(text);
        if (!token.Success || !relay.Success)
        {
            return null;
        }

        var relayBase = Uri.UnescapeDataString(relay.Groups[1].Value).TrimEnd('/');
        var commandBase = RelayCommandUrlPattern.Match(text) is { Success: true } cmd
            ? Uri.UnescapeDataString(cmd.Groups[1].Value).TrimEnd('/')
            : relayBase;
        return new Endpoint(
            new Uri($"{relayBase}/events"),
            new Uri($"{commandBase}/commands"),
            token.Groups[1].Value);
    }

    private static Endpoint? ReadFromRegistry(string sessionDir)
    {
        var registry = Path.Combine(sessionDir, "btui-mvp.registry");
        if (!File.Exists(registry))
        {
            return null;
        }

        try
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(registry));
            var token = RegistryTokenPattern.Match(text);
            if (!token.Success)
            {
                return null;
            }
            // Registry carries the token but not the ports — use the observed defaults.
            return new Endpoint(
                new Uri("http://127.0.0.1:60124/events"),
                new Uri("http://127.0.0.1:60125/commands"),
                token.Value);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
