namespace HeraldHelper.Domain.Models;

/// <summary>
/// Typed projection of the client's adapter registry — everything the
/// client itself renders, decoded into game concepts. Raw adapter keys
/// stay in IAdapterValueSource; this is the model features consume.
/// </summary>
public sealed record ClientStateSnapshot(
    IReadOnlyList<GroupMemberState> GroupMembers,
    IReadOnlyList<string> Buffs,
    IReadOnlyList<string> ConcentrationBuffs,
    PetState? Pet,
    SiegeState? Siege,
    bool InCombat,
    double? CompassHeading,
    long? RealmPoints,
    long? BountyPoints,
    double? RelicTimePercent,
    double? ReleaseTimerSeconds,
    double? TimerSeconds)
{
    public static readonly ClientStateSnapshot Empty = new(
        [], [], [], null, null, false, null, null, null, null, null, null);
}

public sealed record GroupMemberState(
    int Index,
    string Name,
    string Class,
    int? HealthPercent,
    int? PowerPercent,
    int? EndurancePercent,
    int? Level,
    string? Zone,
    double? X,
    double? Y,
    double? Z,
    IReadOnlyList<string> BuffIcons);

public sealed record PetState(
    string? Title,
    int? LifePercent,
    IReadOnlyList<int> CombatPetIndices,
    IReadOnlyList<int> MovingPetIndices);

public sealed record SiegeState(
    double? TimerSeconds,
    bool Moving,
    int? Hits,
    double? HelperTimerSeconds);
