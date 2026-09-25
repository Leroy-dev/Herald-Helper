using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

/// <summary>You got crowd-controlled — "You are stunned!" / "mesmerized!" etc.
/// Incoming CC carries no duration; Elapsed tracking lives in the consumer.</summary>
public sealed record SelfCcEvent(ControlEffectType Effect, int OccurrenceOrdinal);

/// <summary>An enemy attacked you — melee hit, crit, shot, missed swing,
/// or an incoming spell cast on you.</summary>
public sealed record IncomingAttackEvent(
    string Attacker,
    int? Damage,
    bool IsCritical,
    bool Missed,
    bool IsSpellCastOnYou,
    int OccurrenceOrdinal);

public enum CombatLifeKind
{
    Kill,
    Death
}

/// <summary>"You have slain X" / "You have been killed by X" / "You die…"</summary>
public sealed record CombatLifeEvent(CombatLifeKind Kind, string? OtherName, int OccurrenceOrdinal);

/// <summary>"You use X" / "You activate X" — realm-ability activation lines;
/// the consumer maps the name to a known RA + cooldown.</summary>
public sealed record RealmAbilityEvent(string AbilityName, int OccurrenceOrdinal);

/// <summary>"{Name} is stunned!" — a broadcast effect line for a NEARBY
/// player (spell Message2). Applied=true for application lines, false for
/// Message4 expire lines ("{Name} recovers from the stun."). When the name
/// resolves to a group member the group frame can badge the active CC.</summary>
public sealed record BroadcastCcEvent(string Name, ControlEffectType Effect, bool Applied, int OccurrenceOrdinal);

/// <summary>A tracked RA activation with its timestamp (for cooldown timers).
/// CooldownSeconds comes from the charplan delve ("Can use every: 20:00 min")
/// when the catalog provides one — null means elapsed-only display.</summary>
public sealed record RealmAbilityActivation(
    string AbilityName,
    DateTimeOffset UsedUtc,
    int? CooldownSeconds = null);
