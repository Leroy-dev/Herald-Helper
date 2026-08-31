using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public sealed record ShardAuthProfile(
    ShardType Shard,
    string HubUrl,
    string Domain,
    IReadOnlyList<string> PreferredCookieNames);
