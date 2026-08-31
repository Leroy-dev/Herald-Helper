using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public static class ShardAuthSettingsResolver
{
    public static ShardAuthBundle Resolve(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        var name = shard.ToString().ToLowerInvariant();
        settings.TryGetValue($"auth.{name}.cookieHeader", out var cookie);
        settings.TryGetValue($"auth.{name}.userAgent", out var ua);

        if (shard == ShardType.Eden)
        {
            if (string.IsNullOrWhiteSpace(cookie))
            {
                settings.TryGetValue("edenHeraldCookie", out cookie);
            }

            if (string.IsNullOrWhiteSpace(ua))
            {
                settings.TryGetValue("edenHeraldUserAgent", out ua);
            }
        }

        return new ShardAuthBundle(cookie, ua);
    }
}
