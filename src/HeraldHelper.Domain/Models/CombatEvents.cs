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

/// <summary>A tracked RA activation with its timestamp (for cooldown timers).
/// CooldownSeconds comes from the charplan delve ("Can use every: 20:00 min")
/// when the catalog provides one — null means elapsed-only display.</summary>
public sealed record RealmAbilityActivation(
    string AbilityName,
    DateTimeOffset UsedUtc,
    int? CooldownSeconds = null);
