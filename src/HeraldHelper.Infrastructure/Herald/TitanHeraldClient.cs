using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class TitanHeraldClient : IHeraldClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<ShardAuthBundle>? _getAuth;

    public TitanHeraldClient(HttpClient httpClient, Func<ShardAuthBundle>? getAuth = null)
    {
        _httpClient = httpClient;
        _getAuth = getAuth;
    }

    public async Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        var url = $"https://titan.api.opendaoc.com/player/{Uri.EscapeDataString(targetName)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(req);
        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!LooksLikeJson(json))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var guild = root.TryGetProperty("guild", out var guildElement) ? guildElement.GetString() : null;
        var @class = root.TryGetProperty("class", out var classElement) ? classElement.GetString() : null;
        int? level = root.TryGetProperty("level", out var levelElement) && levelElement.TryGetInt32(out var l) ? l : null;
        var rr = root.TryGetProperty("realmRank", out var rrElement) ? rrElement.GetString() : null;
        var solo = SumSoloKills(root);

        return new TargetProfile(targetName, guild, @class, level, rr, solo);
    }

    private static int? SumSoloKills(JsonElement root)
    {
        var total = 0;
        var found = false;
        found |= TryReadInt(root, "killsAlbionSolo", ref total);
        found |= TryReadInt(root, "killsMidgardSolo", ref total);
        found |= TryReadInt(root, "killsHiberniaSolo", ref total);
        return found ? total : null;
    }

    private static bool TryReadInt(JsonElement root, string prop, ref int total)
    {
        if (!root.TryGetProperty(prop, out var e))
        {
            return false;
        }

        if (!e.TryGetInt32(out var value))
        {
            return false;
        }

        total += value;
        return true;
    }

    private void AddAuthHeaders(HttpRequestMessage req)
    {
        var auth = _getAuth?.Invoke();
        if (!string.IsNullOrWhiteSpace(auth?.CookieHeader))
        {
            req.Headers.TryAddWithoutValidation("cookie", auth.CookieHeader);
        }

        if (!string.IsNullOrWhiteSpace(auth?.UserAgent))
        {
            req.Headers.TryAddWithoutValidation("user-agent", auth.UserAgent);
        }
    }

    private static bool LooksLikeJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
