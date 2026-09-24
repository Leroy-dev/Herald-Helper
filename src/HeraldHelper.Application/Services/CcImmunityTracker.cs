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

    public void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc,
        ShardType shard = ShardType.Default)
    {
        if (!hit.LandedSuccessfully)
        {
            return;
        }

        var immunitySeconds = CalculateImmunitySeconds(hit, targetClass, resistPercent, shard);
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
    public static int PreviewImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent,
        ShardType shard = ShardType.Default)
    {
        return CalculateImmunitySeconds(hit, targetClass, resistPercent, shard);
    }

    /// <summary>CC immunity model — OpenDAoC-server-verified
    /// (ECS-Effects/CrowdControlECSEffect.cs): ImmunityDuration =
    /// min(60s, appliedDuration × immunity_timer_adaptive_length) when
    /// adaptive, else flat immunity_timer_flat_length — the deployed
    /// opendaoc.sqlite3 serverproperties have adaptive=False/flat=60, so
    /// every hard CC gets 60s immunity AFTER the effect ends.
    /// EffectHelper.GetImmunityEffectFromSpell maps Mez→MezImmunity,
    /// Stun+StyleStun→StunImmunity (one shared bucket),
    /// SpeedDecrease→SnareImmunity, Nearsight→NearsightImmunity;
    /// DamageSpeedDecrease is absent = no immunity (debuff timer only).
    ///
    /// Handler-level facts (spells/): StyleStun has zero resist chance and
    /// ignores StunDurationReduction — melee stuns land at full duration
    /// regardless of target det/resists on OpenDAoC. Casted Stun, Mez and
    /// SpeedDecrease all multiply the matching *DurationReduction property
    /// (det DOES shorten casted mez). The live-era rule encoded in
    /// StyleStun.OnEffectExpires is 5× stun as post-effect immunity
    /// (camelotherald/more/1749) — Eden uses that measured/live model.
    /// The shard arg selects: Eden = 5× melee + det, others = flat 60s.</summary>
    private static int CalculateImmunitySeconds(AbilityHit hit, string? targetClass, int resistPercent, ShardType shard)
    {
        var ccLength = hit.BaseDurationSeconds;

        if (hit.EffectType is ControlEffectType.Snare)
        {
            return Math.Max(1, ccLength);
        }

        if (hit.EffectType is ControlEffectType.Nearsight)
        {
            // NearsightImmunity is a real server immunity — flat 60s
            // post-effect; no det property exists for nearsight.
            var nearEffective = (int)Math.Floor(ccLength * (0.74 - resistPercent / 100.0));
            return Math.Max(1, nearEffective + 60);
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

        if (hit.IsMeleeStyle)
        {
            if (shard == ShardType.Eden)
            {
                // Live-era rule: 5× the applied stun as immunity after it
                // ends (herald patch note). Det still shortens the stun on
                // Eden as far as anyone has measured.
                var applied = (int)Math.Floor(ccLength * detMultiplier);
                return Math.Max(1, applied + applied * 5);
            }

            // OpenDAoC: StyleStun ignores det and resists — full duration,
            // then flat 60s immunity. Slam 9s → 69s total.
            return Math.Max(1, ccLength + 60);
        }

        var baseMultiplier = 0.74 - (resistPercent / 100.0);
        var effective = (int)Math.Floor(ccLength * baseMultiplier * detMultiplier);
        return Math.Max(1, effective + 60);
    }
}
