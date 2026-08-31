namespace HeraldHelper.Infrastructure.Configuration;

public sealed record HeraldRuntimeConfig(
    string? EdenCookieHeader,
    string? EdenUserAgent)
{
    public static HeraldRuntimeConfig LoadFromCfg(string cfgPath)
    {
        if (!File.Exists(cfgPath))
        {
            return new HeraldRuntimeConfig(null, null);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(cfgPath))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            map[parts[0].Trim()] = parts[1].Trim();
        }

        return FromMap(map);
    }

    public static HeraldRuntimeConfig FromMap(IReadOnlyDictionary<string, string> map)
    {
        map.TryGetValue("edenHeraldCookie", out var cookie);
        map.TryGetValue("edenHeraldUserAgent", out var userAgent);
        return new HeraldRuntimeConfig(cookie, userAgent);
    }
}
