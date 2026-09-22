namespace HeraldHelper.Domain.Models;

public sealed record CastSpellInfo(
    string SpellName,
    double CastTimeSeconds,
    IconSpriteRef? Icon,
    double? BaseDamage = null,
    string? DamageType = null,
    string? ClassName = null,
    int? Level = null,
    bool IsFixedCastTime = false,
    double? RecastSeconds = null,
    double? DurationSeconds = null,
    string? SpellType = null,
    string? CastTarget = null,
    bool UsesConcentration = false)
{
    /// <summary>Anything that lands on a friendly target and stays — stat/resist/
    /// regen/speed buffs, damage shields, bladeturns, ablatives, pet spells.</summary>
    public bool IsBuffEffect =>
        SpellType is { } type &&
        (type.Contains("Buff", StringComparison.OrdinalIgnoreCase)
         || type.Contains("Shield", StringComparison.OrdinalIgnoreCase)
         || type.Contains("Ablative", StringComparison.OrdinalIgnoreCase)
         || type.Contains("Bladeturn", StringComparison.OrdinalIgnoreCase)
         || type.Contains("Speed Enhancement", StringComparison.OrdinalIgnoreCase)
         || type.Contains("SpeedEnhancement", StringComparison.OrdinalIgnoreCase)
         || type.Contains("Pet", StringComparison.OrdinalIgnoreCase));
}
