using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Parsing;

public sealed record AbilityDefinition(
    string Name,
    string SkillCode,
    int DurationSeconds,
    ControlEffectType EffectType,
    IReadOnlyList<string>? Aliases = null);
