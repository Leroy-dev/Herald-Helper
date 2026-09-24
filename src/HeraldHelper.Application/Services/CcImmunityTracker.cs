using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public sealed class CcImmunityTracker : ICcImmunityTracker
{
    private static readonly HashSet<string> LightDetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Berserker", "Skald", "Savage", "Thane", "Shadowblade", "Hunter",
        "Reaver", "Friar", "Paladin", "Infiltrator", "Scout", "Champion",
        "Warden", "Nightshade", "Ranger", "Valewalker"
    };

    private static readonly HashSet<string> DetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Warrior", "Mercenary", "Armswoman", "Armsman", "Blademaster", "Hero", "Heroine", "Valkyrie"
    };

    private readonly List<CcTimerEntry> _entries = [];

    public void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc)
    {
        if (!hit.LandedSuccessfully)
        {
            return;
        }

        var immunitySeconds = CalculateImmunitySeconds(hit, targetClass, resistPercent);
        _entries.RemoveAll(x => x.TargetName == hit.TargetName && x.EffectType == hit.EffectType);
        var expiresAtUtc = nowUtc.AddSeconds(immunitySeconds);
        _entries.Add(new CcTimerEntry(hit.TargetName, hit.EffectType, expiresAtUtc, hit.Icon, targetClass, nowUtc));
    }

    public void RetractFreshEntries(IEnumerable<string> targetNames, DateTimeOffset nowUtc, TimeSpan maxAge)
    {
        var names = new HashSet<string>(
            targetNames.Select(NormalizeTargetName).Where(x => x.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
        {
            return;
        }

        _entries.RemoveAll(x =>
            x.CreatedUtc is { } created &&
            nowUtc - created <= maxAge &&
            names.Contains(NormalizeTargetName(x.TargetName)));
    }

    /// <summary>"The goborchend wounder" and "goborchend wounder" are the
    /// same target — chat messages disagree on the leading article. The
    /// adapter layer also appends "---" to non-player target names.</summary>
    private static string NormalizeTargetName(string name)
    {
        var trimmed = name.Trim().TrimEnd('-').TrimEnd();
        return trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..].TrimStart()
            : trimmed;
    }

    public IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc)
    {
        _entries.RemoveAll(x => x.RemainingSeconds(nowUtc) <= 0);
        return _entries.AsReadOnly();
    }

    /// <summary>What the immunity window would be for this hit — used by the
    /// abilities "test line" so users can verify durations without a client.</summary>
    public static int PreviewImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent)
    {
        return CalculateImmunitySeconds(hit, targetClass, resistPercent);
    }

    /// <summary>Eden CC model (user-confirmed): casted CC — spells and
    /// songs — grants a flat 60s immunity that starts when the effect ends,
    /// so the timer is appliedDuration + 60. Melee/style CC grants immunity
    /// of 6x the stun after it ends — Slam 9s is 9s stun + 54s immunity =
    /// 63s total. Debuffs without immunity (nearsight, damage+snare) track
    /// the effect duration only.</summary>
    private static int CalculateImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent)
    {
        var ccLength = hit.BaseDurationSeconds;

        if (hit.EffectType is ControlEffectType.Nearsight or ControlEffectType.Snare)
        {
            return Math.Max(1, ccLength);
        }

        var detMultiplier = 1.0;
        if (!string.IsNullOrWhiteSpace(targetClass))
        {
            if (DetClasses.Contains(targetClass))
            {
                detMultiplier = 0.2;
            }
            else if (LightDetClasses.Contains(targetClass))
            {
                detMultiplier = 0.45;
            }
        }

        // Melee style stuns land at their listed duration — only
        // Determination cuts them, spell resists do not apply.
        if (hit.IsMeleeStyle)
        {
            var applied = (int)Math.Floor(ccLength * detMultiplier);
            return Math.Max(1, applied + applied * 6);
        }

        // Casted CC lands at the spell-resist fraction (0.74 base on Eden
        // PvP); mez ignores Determination.
        var baseMultiplier = 0.74 - (resistPercent / 100.0);
        var effective = hit.EffectType == ControlEffectType.Mezz
            ? (int)Math.Floor(ccLength * baseMultiplier)
            : (int)Math.Floor(ccLength * baseMultiplier * detMultiplier);
        return Math.Max(1, effective + 60);
    }
}
