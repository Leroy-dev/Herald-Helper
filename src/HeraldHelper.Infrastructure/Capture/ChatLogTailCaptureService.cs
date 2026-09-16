using System.Text;
using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Experimental chat source: tails the DAoC client's chat.log (enabled in game via
/// /chatlog) instead of OCR. The client keeps the file open and flushes in ~4KB
/// blocks, so reads use full sharing and only complete lines are emitted; the
/// "[HH:MM:SS] " prefix is stripped before the shared chat-event parser sees text.
/// Non-chat watch regions still route to the OCR fallback.
/// </summary>
public sealed class ChatLogTailCaptureService : IChatCaptureService, IWindowAwareChatCaptureService, IAdapterValueSource, IChatCaptureSourceTelemetry
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[\d{1,2}:\d{2}:\d{2}\]\s?",
        RegexOptions.Compiled);
    private static readonly Regex AdapterLine = new(
        @"^(?<name>\S+) \((?:scalar|text)\): ""?(?<value>[^""\r\n]*)""?\s*$",
        RegexOptions.Compiled);
    private const string UnknownAdapterPrefix = "There are no scalar or text adapters named:";

    private readonly IWindowAwareChatCaptureService _ocrFallback;
    private readonly string? _configuredPath;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly DaocChatLogPump? _pump;
    private readonly HashSet<string> _regionKeys = new(StringComparer.OrdinalIgnoreCase) { "chat" };
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _adapterValues = new(StringComparer.OrdinalIgnoreCase);
    private string? _resolvedPath;
    private long _offset;
    private string _pendingPartial = string.Empty;
    private bool _attached;

    public ChatLogTailCaptureService(
        IWindowAwareChatCaptureService ocrFallback,
        string? configuredPath = null,
        IEnumerable<string>? regionKeys = null,
        IResponseDiagnostics? diagnostics = null,
        DaocChatLogPump? pump = null)
    {
        _ocrFallback = ocrFallback;
        _configuredPath = string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath.Trim();
        if (regionKeys is not null)
        {
            foreach (var key in regionKeys.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                _regionKeys.Add(key.Trim());
            }
        }
        _diagnostics = diagnostics;
        _pump = pump;
    }

    public string? ResolvedPath
    {
        get { lock (_sync) return _resolvedPath; }
    }

    public long Offset
    {
        get { lock (_sync) return _offset; }
    }

    public IReadOnlyDictionary<string, string> LatestAdapterValues
    {
        get { lock (_sync) return new Dictionary<string, string>(_adapterValues, StringComparer.OrdinalIgnoreCase); }
    }

    public string LastChatSource => "chat.log";

    public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken) =>
        Task.FromResult(ReadNewLines());

    public Task<string> CaptureWindowTextAsync(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken) =>
        _regionKeys.Contains(watchRegion.Key) || _regionKeys.Contains(watchRegion.Label)
            ? Task.FromResult(ReadNewLines())
            : _ocrFallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);

    private string ReadNewLines()
    {
        _pump?.Pump();
        lock (_sync)
        {
            var path = ResolvePath();
            if (path is null)
            {
                return string.Empty;
            }

            try
            {
                // The client holds the log open for the whole session and flushes
                // its CRT buffer in ~4KB blocks, so reopen per poll with full
                // sharing and tolerate replace/delete between polls.
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var length = stream.Length;
                if (length < _offset)
                {
                    _offset = 0;
                    _pendingPartial = string.Empty;
                }

                if (!_attached)
                {
                    _offset = length;
                    _attached = true;
                    _diagnostics?.Log($"[ChatLog] attached {path} at offset {_offset}");
                    return string.Empty;
                }

                if (length == _offset)
                {
                    return string.Empty;
                }

                stream.Seek(_offset, SeekOrigin.Begin);
                var buffer = new byte[length - _offset];
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = stream.Read(buffer, read, buffer.Length - read);
                    if (n == 0)
                    {
                        break;
                    }
                    read += n;
                }
                _offset += read;
                if (read == 0)
                {
                    return string.Empty;
                }

                var text = _pendingPartial + Encoding.UTF8.GetString(buffer, 0, read);
                var lastNewline = text.LastIndexOf('\n');
                if (lastNewline < 0)
                {
                    _pendingPartial = text;
                    return string.Empty;
                }

                _pendingPartial = text[(lastNewline + 1)..];
                var lines = text[..lastNewline].Split('\n');
                var sb = new StringBuilder(text.Length);
                foreach (var raw in lines)
                {
                    var line = TimestampPrefix.Replace(raw.TrimEnd('\r'), string.Empty);
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    // /showadapter output is telemetry, not chat — stash it in
                    // the adapter table and keep it out of the event parser.
                    var adapterMatch = AdapterLine.Match(line);
                    if (adapterMatch.Success)
                    {
                        RecordAdapterValue(adapterMatch.Groups["name"].Value, adapterMatch.Groups["value"].Value);
                        continue;
                    }
                    if (line.StartsWith(UnknownAdapterPrefix, StringComparison.Ordinal))
                    {
                        _diagnostics?.Log($"[ChatLog] {line}");
                        continue;
                    }

                    sb.Append(line).Append('\n');
                }
                return sb.ToString();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }
    }

    private void RecordAdapterValue(string name, string value)
    {
        if (_adapterValues.TryGetValue(name, out var existing) && existing == value)
        {
            return;
        }
        _adapterValues[name] = value;
        _diagnostics?.Log($"[ChatLog] adapter {name} = {value}");
    }

    private string? ResolvePath()
    {
        if (_resolvedPath is not null)
        {
            if (File.Exists(_resolvedPath))
            {
                return _resolvedPath;
            }

            _diagnostics?.Log($"[ChatLog] {_resolvedPath} removed; re-probing");
            _resolvedPath = null;
            _offset = 0;
            _pendingPartial = string.Empty;
            _attached = false;
        }

        foreach (var candidate in Candidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            _resolvedPath = candidate;
            _offset = 0;
            _pendingPartial = string.Empty;
            _attached = false;
            _diagnostics?.Log($"[ChatLog] resolved {candidate}");
            return candidate;
        }

        return null;
    }

    private IEnumerable<string> Candidates()
    {
        // An explicit override is exclusive: a wrong configured path must surface
        // as "no lines" rather than silently tailing a different install's log.
        if (_configuredPath is not null)
        {
            yield return _configuredPath;
            yield break;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            yield return Path.Combine(documents, "Electronic Arts", "Dark Age of Camelot", "chat.log");
        }

        yield return Path.Combine(Environment.CurrentDirectory, "chat.log");
        yield return Path.Combine(AppContext.BaseDirectory, "chat.log");
    }
}
