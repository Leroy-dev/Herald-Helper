using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class PhoenixHeraldClient : IHeraldClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<ShardAuthBundle>? _getAuth;

    public PhoenixHeraldClient(HttpClient httpClient, Func<ShardAuthBundle>? getAuth = null)
    {
        _httpClient = httpClient;
        _getAuth = getAuth;
    }

    public async Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        var url = $"https://herald.playphoenix.online/c/{Uri.EscapeDataString(targetName)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(req);
        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(cancellationToken);

        var name = TryLabel(html, "Name") ?? targetName;
        var guild = TryLabel(html, "Guild");
        var @class = TryLabel(html, "Class");
        var levelText = TryLabel(html, "Level");
        var rr = TryLabel(html, "Realm Rank");
        var soloText = TryLabel(html, "Solo Kills");

        int? level = int.TryParse(levelText, out var lv) ? lv : null;
        int? solo = int.TryParse((soloText ?? string.Empty).Replace(",", string.Empty), out var sk) ? sk : null;
        return new TargetProfile(name, guild, @class, level, rr, solo);
    }

    private static string? TryLabel(string html, string label)
    {
        var pattern = $@"{Regex.Escape(label)}\s*</[^>]+>\s*<[^>]+>(.*?)</";
        var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? StripTags(m.Groups[1].Value).Trim() : null;
    }

    private static string StripTags(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
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
}
