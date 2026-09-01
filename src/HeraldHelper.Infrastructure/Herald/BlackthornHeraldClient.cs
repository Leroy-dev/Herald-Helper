using System.Net;
using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Infrastructure.Herald;

public sealed partial class BlackthornHeraldClient : IHeraldClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<ShardAuthBundle>? _getAuth;

    public BlackthornHeraldClient(HttpClient httpClient, Func<ShardAuthBundle>? getAuth = null)
    {
        _httpClient = httpClient;
        _getAuth = getAuth;
    }

    public async Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        var url = $"https://herald.blackthorn-daoc.com/stats/player/{Uri.EscapeDataString(targetName)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(req);
        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(cancellationToken);

        var guild = MatchOne(html, @"<a\b[^>]*href=""/stats/guild/[^""]+""[^>]*>(.*?)</a>");
        var @class = MatchOne(html, @"<td\b[^>]*class=""[^""]*\bclassName\b[^""]*""[^>]*>.*?<a\b[^>]*href=""/stats/players/realmpoints/[^""]+""[^>]*>(.*?)</a>");
        var levelText = MatchOne(html, @"<td\b[^>]*class=""[^""]*\bLevel\b[^""]*""[^>]*>(.*?)</td>");
        var rr = MatchOne(html, @"<td\b[^>]*class=""[^""]*\bRR\b[^""]*""[^>]*>(.*?)</td>");
        var solo = MatchStatAllTime(html, "Solo");

        int? level = int.TryParse(levelText, out var levelValue) ? levelValue : null;
        return new TargetProfile(targetName, guild, @class, level, rr, solo);
    }

    private static string? MatchOne(string input, string pattern, int captureIndex = 1)
    {
        var m = Regex.Match(input, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? CleanText(m.Groups[captureIndex].Value) : null;
    }

    private static int? MatchStatAllTime(string input, string label)
    {
        var pattern = $@"<tr\b[^>]*>.*?<td\b[^>]*>\s*{Regex.Escape(label)}\s*</td>(.*?)</tr>";
        var row = Regex.Match(input, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!row.Success)
        {
            return null;
        }

        var values = Regex.Matches(row.Groups[1].Value, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Select(x => CleanText(x.Groups[1].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
            .ToList();
        if (values.Count == 0)
        {
            return null;
        }

        var raw = values[^1].Replace(",", string.Empty, StringComparison.Ordinal);
        return int.TryParse(raw, out var value) ? value : null;
    }

    private static string CleanText(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(withoutTags).Trim();
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
