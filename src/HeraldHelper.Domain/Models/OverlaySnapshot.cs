using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

public sealed record OverlaySnapshot(
    TargetProfile? Target,
    IReadOnlyCollection<CcTimerEntry> Timers,
    CastBarState? ActiveCast,
    string RawOcrText,
    SelfCcState? SelfCc = null,
    IReadOnlyCollection<PeelEntry>? RecentAttackers = null,
    IReadOnlyCollection<CooldownEntry>? Cooldowns = null,
    IReadOnlyCollection<TrackedBuff>? TrackedBuffs = null,
    ClientStateSnapshot? ClientState = null,
    DateTimeOffset? CastInterruptedUntil = null);

/// <summary>You are currently crowd-controlled — started when the chat line
/// fired; incoming CC has no duration so the overlay shows elapsed time.</summary>
public sealed record SelfCcState(ControlEffectType Effect, DateTimeOffset StartedUtc);

/// <summary>An enemy who attacked you recently (peel tracking).</summary>
public sealed record PeelEntry(string Attacker, int HitCount, DateTimeOffset LastSeenUtc);

/// <summary>A spell-recast or realm-ability cooldown counting down in the
/// timers window. ReadyUtc when the remaining time is known; UsedUtc-only
/// entries render as "used Xm ago".</summary>
public sealed record CooldownEntry(
    string Name,
    DateTimeOffset UsedUtc,
    DateTimeOffset? ReadyUtc = null,
    IconSpriteRef? Icon = null);

/// <summary>A buff applied to you or your pet — tracked from cast chat lines,
/// expiry from catalog duration. Concentration buffs have no ExpiresAtUtc
/// (they persist until released/faded).</summary>
public sealed record TrackedBuff(
    string Name,
    DateTimeOffset AppliedUtc,
    DateTimeOffset? ExpiresAtUtc = null,
    bool OnPet = false,
    IconSpriteRef? Icon = null);
