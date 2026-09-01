using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Desktop;

public static class ShardAuthProfileResolver
{
    public static ShardAuthProfile? Resolve(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        var key = shard.ToString().ToLowerInvariant();
        if (settings.TryGetValue($"auth.{key}.enabled", out var enabledRaw) &&
            enabledRaw.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var defaultProfile = DefaultProfile(shard);
        var hub = Get(settings, $"auth.{key}.hubUrl", defaultProfile?.HubUrl);
        var domain = Get(settings, $"auth.{key}.domain", defaultProfile?.Domain);
        var cookieNamesCsv = Get(settings, $"auth.{key}.cookieNames", defaultProfile is null ? null : string.Join(",", defaultProfile.PreferredCookieNames));

        if (string.IsNullOrWhiteSpace(hub) || string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var names = (cookieNamesCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new ShardAuthProfile(shard, hub, domain, names);
    }

    public static IEnumerable<ConfigEntry> DefaultSettings()
    {
        yield return new ConfigEntry { Key = "auth.eden.enabled", Value = "true" };
        yield return new ConfigEntry { Key = "auth.eden.hubUrl", Value = "https://eden-daoc.net/hub" };
        yield return new ConfigEntry { Key = "auth.eden.domain", Value = "eden-daoc.net" };
        yield return new ConfigEntry { Key = "auth.eden.cookieNames", Value = "eden_daoc_u,eden_daoc_k,eden_daoc_sid" };
        yield return new ConfigEntry { Key = "auth.eden.cookieHeader", Value = "" };
        yield return new ConfigEntry { Key = "auth.eden.userAgent", Value = "" };
        yield return new ConfigEntry { Key = "auth.phoenix.enabled", Value = "false" };
        yield return new ConfigEntry { Key = "auth.titan.enabled", Value = "false" };
        yield return new ConfigEntry { Key = "auth.celestius.enabled", Value = "false" };
        yield return new ConfigEntry { Key = "auth.blackthorn.enabled", Value = "false" };
    }

    private static ShardAuthProfile? DefaultProfile(ShardType shard)
    {
        return shard switch
        {
            ShardType.Eden => new ShardAuthProfile(ShardType.Eden, "https://eden-daoc.net/hub", "eden-daoc.net", ["eden_daoc_u", "eden_daoc_k", "eden_daoc_sid"]),
            _ => null
        };
    }

    private static string? Get(IReadOnlyDictionary<string, string> map, string key, string? fallback)
    {
        return map.TryGetValue(key, out var value) ? value : fallback;
    }
}
