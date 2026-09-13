namespace HeraldHelper.Domain.Models;

/// <summary>
/// A cached target-profile row for browsing/search — carries the server so
/// same-named players on different shards stay distinct.
/// </summary>
public sealed record TargetProfileRow(
    string Server,
    string Name,
    string? Guild,
    string? Class,
    int? Level,
    string? RealmRank,
    int? SoloKills,
    string UpdatedUtc);
