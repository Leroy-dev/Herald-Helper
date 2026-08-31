using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

public sealed class OverrideAwareCastSpellCatalog : ICastSpellCatalog
{
    private readonly ICastSpellCatalog _baseCatalog;
    private readonly Func<IReadOnlyCollection<CastSpellOverride>> _getOverrides;

    public OverrideAwareCastSpellCatalog(
        ICastSpellCatalog baseCatalog,
        Func<IReadOnlyCollection<CastSpellOverride>> getOverrides)
    {
        _baseCatalog = baseCatalog;
        _getOverrides = getOverrides;
    }

    public CastSpellInfo? FindBySpellName(string spellName)
    {
        return FindBySpellName(spellName, null, null);
    }

    public CastSpellInfo? FindBySpellName(string spellName, string? className, int? level)
    {
        if (string.IsNullOrWhiteSpace(spellName))
        {
            return null;
        }

        var normalizedName = NormalizeName(spellName);
        var matchingOverride = _getOverrides()
            .Where(x => string.Equals(NormalizeName(x.SourceName), normalizedName, StringComparison.Ordinal)
                || string.Equals(NormalizeName(x.Name), normalizedName, StringComparison.Ordinal))
            .Where(x => string.IsNullOrWhiteSpace(className)
                || string.IsNullOrWhiteSpace(x.ClassName)
                || string.Equals(x.ClassName, className, StringComparison.OrdinalIgnoreCase))
            .Where(x => level is null || x.Level is null || x.Level <= level)
            .OrderByDescending(x => string.Equals(x.ClassName, className, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Level ?? -1)
            .ThenByDescending(x => string.Equals(NormalizeName(x.Name), normalizedName, StringComparison.Ordinal))
            .ThenByDescending(x => x.Icon is not null)
            .ThenByDescending(x => x.CastTimeSeconds ?? -1)
            .FirstOrDefault();

        if (matchingOverride is null)
        {
            return _baseCatalog.FindBySpellName(spellName, className, level);
        }

        var baseInfo = _baseCatalog.FindBySpellName(matchingOverride.SourceName, className, level);

        return matchingOverride.CastTimeSeconds is > 0
            ? new CastSpellInfo(
                matchingOverride.Name.Trim(),
                matchingOverride.CastTimeSeconds.Value,
                matchingOverride.Icon,
                baseInfo?.BaseDamage,
                baseInfo?.DamageType,
                matchingOverride.ClassName,
                matchingOverride.Level,
                baseInfo?.IsFixedCastTime ?? false)
            : null;
    }

    private static string NormalizeName(string value)
    {
        return string.Join(" ", value
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }
}
