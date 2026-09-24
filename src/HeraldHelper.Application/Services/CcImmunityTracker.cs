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

    private static int CalculateImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent)
    {
        // EffectType is the authoritative CC class — SkillCode is a trigger
        // label ('s'/'m' for spell vs melee line in hand-edited profiles) and
        // mixes the two in real configs.
        var ccLength = hit.BaseDurationSeconds;
        var baseMultiplier = 0.74 - (resistPercent / 100.0);
        var ccLengthModifier = 10;

        // Nearsight is a debuff, not hard CC — no immunity window, the
        // timer just tracks the debuff duration itself.
        if (hit.EffectType == ControlEffectType.Nearsight)
        {
            return Math.Max(1, ccLength);
        }

        if (hit.EffectType == ControlEffectType.Mezz)
        {
            ccLengthModifier = 6;
        }
        else
        {
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

            ccLength = (int)Math.Floor(ccLength * baseMultiplier * detMultiplier);
        }

        var total = ccLength * ccLengthModifier;
        if (total >= 60 || hit.EffectType == ControlEffectType.Stun)
        {
            total = 60 + ccLength;
        }
        else
        {
            total += ccLength;
        }

        return Math.Max(1, total);
    }
}
