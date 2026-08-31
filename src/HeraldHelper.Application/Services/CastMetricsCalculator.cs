using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public static class CastMetricsCalculator
{
    private const double MinimumAdjustedCastSeconds = 2;

    public static double CalculateCastTime(
        double baseSeconds,
        CharacterStatsSnapshot? stats,
        bool enabled,
        bool isFixedCastTime = false)
    {
        if (isFixedCastTime || !enabled || stats?.Dexterity is null || baseSeconds <= 0)
        {
            return baseSeconds;
        }

        var dexterityReduction = Math.Clamp((stats.Dexterity.Value - 60) / 600d, 0, 0.6);
        var itemReduction = Math.Clamp(stats.CastingSpeedPercent / 100d, 0, 0.25);
        var adjusted = baseSeconds * (1 - dexterityReduction) * (1 - itemReduction);
        // DAoC does not reduce normal casts below two seconds. Preserve a
        // naturally shorter catalogued cast instead of ever making it slower.
        var minimum = Math.Min(baseSeconds, MinimumAdjustedCastSeconds);
        return Math.Round(Math.Max(minimum, adjusted), 3);
    }

    public static double? EstimateDamage(
        CastSpellInfo spell,
        CharacterStatsSnapshot? stats,
        string? className,
        bool enabled)
    {
        if (!enabled || spell.BaseDamage is null || spell.BaseDamage <= 0)
        {
            return null;
        }

        var acuity = stats?.ResolveAcuity(className);
        var acuityFactor = acuity is null ? 1 : 1 + Math.Clamp((acuity.Value - 60) / 600d, 0, 0.75);
        var itemFactor = 1 + Math.Clamp((stats?.SpellDamagePercent ?? 0) / 100d, 0, 0.25);
        return Math.Round(spell.BaseDamage.Value * acuityFactor * itemFactor);
    }
}
