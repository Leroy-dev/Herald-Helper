using HeraldHelper.Application.Contracts;
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
        _entries.Add(new CcTimerEntry(hit.TargetName, hit.EffectType, expiresAtUtc));
    }

    public IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc)
    {
        _entries.RemoveAll(x => x.RemainingSeconds(nowUtc) <= 0);
        return _entries.AsReadOnly();
    }

    private static int CalculateImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent)
    {
        var ccLength = hit.BaseDurationSeconds;
        var skillCode = hit.SkillCode.Trim().ToLowerInvariant();
        var baseMultiplier = 0.74 - (resistPercent / 100.0);
        var ccLengthModifier = 10;

        if (skillCode == "m")
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
        if (total >= 60 || skillCode == "s")
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
