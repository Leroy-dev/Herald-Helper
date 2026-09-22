using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace HeraldHelper.Desktop;

/// <summary>GitHub releases check — the app ships self-contained, so users
/// have no updater; this at least tells them a newer build exists.</summary>
public static class UpdateCheckService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<string> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/Leroy-dev/Herald-Helper/releases/latest");
            request.Headers.UserAgent.ParseAdd("HeraldHelper");
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "No releases published yet."
                    : $"Update check failed: HTTP {(int)response.StatusCode}";
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return "No releases published yet.";
            }

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            var remoteVersionText = tag.Trim().TrimStart('v', 'V');
            if (Version.TryParse(remoteVersionText, out var remote) && remote > current)
            {
                return $"Update available: {tag} (you're on {current.ToString(3)}) — {url}";
            }

            return $"Latest release is {tag} — you're on {current.ToString(3)}.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"Update check failed: {ex.Message}";
        }
    }
}
