namespace HeraldHelper.Domain.Models;

/// <summary>
/// Typed projection of the client's adapter registry — everything the
/// client itself renders, decoded into game concepts. Raw adapter keys
/// stay in IAdapterValueSource; this is the model features consume.
/// </summary>
public sealed record ClientStateSnapshot(
    IReadOnlyList<GroupMemberState> GroupMembers,
    IReadOnlyList<string> Buffs,
    IReadOnlyList<SelfEffect> SelfEffects,
    PetState? Pet,
    SiegeState? Siege,
    bool InCombat,
    double? CompassHeading,
    long? RealmPoints,
    long? BountyPoints,
    double? RelicTimePercent,
    double? ReleaseTimerSeconds,
    double? TimerSeconds,
    PlayerVitals? Vitals = null,
    int? TargetHealthPercent = null)
{
    public static readonly ClientStateSnapshot Empty = new(
        [], [], [], null, null, false, null, null, null, null, null, null);
}

/// <summary>The player's own vitals and resists, read live from the
/// client's adapter registry — real values, not OCR guesses.
/// Resists arrive as rendered "+15%"/"-20%" strings.</summary>
public sealed record PlayerVitals(
    int? HealthPercent,
    int? PowerPercent,
    int? EndurancePercent,
    IReadOnlyDictionary<string, int> Resists);

/// <summary>One active effect on the player, read from the client's
/// "EFFECTS" array — real name + iconId, live from memory.</summary>
public sealed record SelfEffect(string Name, int IconId);

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
    IReadOnlyList<int> MovingPetIndices,
    IReadOnlyList<int> EffectIconIds);

public sealed record SiegeState(
    double? TimerSeconds,
    bool Moving,
    int? Hits,
    double? HelperTimerSeconds);
