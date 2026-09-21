using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

public sealed record AbilityHit(
    string TargetName,
    string AbilityName,
    string SkillCode,
    ControlEffectType EffectType,
    int BaseDurationSeconds,
    bool LandedSuccessfully,
    int OccurrenceOrdinal = 1,
    IconSpriteRef? Icon = null);
