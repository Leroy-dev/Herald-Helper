using System.Text;
using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class CelestiusHeraldClient : IHeraldClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<ShardAuthBundle>? _getAuth;
    private const string Endpoint = "https://s695ojsti6.execute-api.eu-west-1.amazonaws.com/dev/graphql";

    public CelestiusHeraldClient(HttpClient httpClient, Func<ShardAuthBundle>? getAuth = null)
    {
        _httpClient = httpClient;
        _getAuth = getAuth;
    }

    public async Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            query = "query CharacterScreenQuery($name: String!) { character(name: $name) { name class { name } guild { name } realmRank rvrStats { soloKills } } }",
            variables = new { name = targetName }
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        AddAuthHeaders(req);
        using var response = await _httpClient.SendAsync(req, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!LooksLikeJson(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("character", out var character))
        {
            return null;
        }

        if (character.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var guild = TryGetNestedString(character, "guild", "name");
        var @class = TryGetNestedString(character, "class", "name");
        var rr = character.TryGetProperty("realmRank", out var rrEl) ? rrEl.ToString() : null;
        int? solo = character.TryGetProperty("rvrStats", out var rvr)
            && rvr.TryGetProperty("soloKills", out var soloEl)
            && soloEl.TryGetInt32(out var soloInt)
            ? soloInt
            : null;

        return new TargetProfile(targetName, guild, @class, null, rr, solo);
    }

    private static string? TryGetNestedString(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out var p) || p.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return p.TryGetProperty(child, out var c) ? c.GetString() : null;
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
