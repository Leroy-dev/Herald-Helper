namespace HeraldHelper.Domain.Models;

public sealed record CastSpellInfo(
    string SpellName,
    double CastTimeSeconds,
    IconSpriteRef? Icon,
    double? BaseDamage = null,
    string? DamageType = null,
    string? ClassName = null,
    int? Level = null,
    bool IsFixedCastTime = false);
