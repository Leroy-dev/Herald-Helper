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
    IconSpriteRef? Icon = null,
    /// True when the CC came from a melee style ("You perform your Slam
    /// perfectly!") — style immunity is 6x the stun, casted CC gets a flat
    /// 60s on top of the effect duration.
    bool IsMeleeStyle = false);
