using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Parsing;

public sealed record AbilityDefinition(
    string Name,
    string SkillCode,
    int DurationSeconds,
    ControlEffectType EffectType,
    IReadOnlyList<string>? Aliases = null,
    IconSpriteRef? Icon = null);
