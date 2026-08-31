namespace HeraldHelper.Domain.Models;

public sealed record TargetProfile(
    string Name,
    string? Guild,
    string? Class,
    int? Level,
    string? RealmRank,
    int? SoloKills)
{
    public bool IsLoading { get; init; }
}
