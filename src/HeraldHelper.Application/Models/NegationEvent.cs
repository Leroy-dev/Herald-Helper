namespace HeraldHelper.Application.Models;

public enum NegationKind
{
    /// "{name} resists the effect!/charm!" — the spell fired but was resisted.
    Resisted,
    /// "Your target is immune to this effect!" / "enraged and resists" /
    /// item-effect intercepts — the spell was negated entirely.
    Immune,
    /// Your melee swing missed / was blocked / parried / evaded / absorbed /
    /// intercepted — a prepared style is consumed without effect.
    SwingFailed,
    /// "You fail to execute your X perfectly!" — the swing hit but the
    /// style did not fire, or the style queue was cancelled.
    StyleFailed,
    /// "{name} already has this effect!" / "can't have that effect again
    /// yet!" — the attempt was rejected because the CC is already running;
    /// the existing timer stays valid, only a brand-new one is wrong.
    FailedApplication
}

/// <summary>A chat observation that can retract a CC timer which was
/// created moments earlier — resists and immune messages often scroll in a
/// tick after the "You cast …" / "You perform …" line.</summary>
public sealed record NegationEvent(
    NegationKind Kind,
    string? TargetName,
    int OccurrenceOrdinal = 1);
