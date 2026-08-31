namespace HeraldHelper.Infrastructure.Auth;

public sealed record ShardAuthBundle(string? CookieHeader, string? UserAgent);
