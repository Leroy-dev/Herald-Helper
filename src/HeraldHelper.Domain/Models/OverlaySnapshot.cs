using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

public sealed record OverlaySnapshot(
    TargetProfile? Target,
    IReadOnlyCollection<CcTimerEntry> Timers,
    CastBarState? ActiveCast,
    string RawOcrText,
    SelfCcState? SelfCc = null,
    IReadOnlyCollection<PeelEntry>? RecentAttackers = null,
    IReadOnlyCollection<RealmAbilityActivation>? RecentRealmAbilityUses = null,
    ClientStateSnapshot? ClientState = null,
    SessionCombatStats? CombatStats = null,
    DateTimeOffset? CastInterruptedUntil = null);

/// <summary>You are currently crowd-controlled — started when the chat line
/// fired; incoming CC has no duration so the overlay shows elapsed time.</summary>
public sealed record SelfCcState(ControlEffectType Effect, DateTimeOffset StartedUtc);

/// <summary>An enemy who attacked you recently (peel tracking).</summary>
public sealed record PeelEntry(string Attacker, int HitCount, DateTimeOffset LastSeenUtc);

/// <summary>Session kill/death counters from parsed combat lines.</summary>
public sealed record SessionCombatStats(
    int Kills,
    int Deaths,
    int HitsTaken,
    int RealmAbilityUses,
    DateTimeOffset StartedUtc);
