using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Models;

public sealed record ChatParseResult(
    TargetEvent? TargetEvent,
    IReadOnlyCollection<AbilityHit> AbilityHits,
    CastEvent? CastEvent = null,
    IReadOnlyCollection<TargetEvent>? VisibleTargetEvents = null,
    IReadOnlyCollection<CastEvent>? VisibleCastEvents = null,
    IReadOnlyCollection<SelfCcEvent>? SelfCcEvents = null,
    IReadOnlyCollection<IncomingAttackEvent>? IncomingAttacks = null,
    IReadOnlyCollection<CombatLifeEvent>? LifeEvents = null,
    IReadOnlyCollection<RealmAbilityEvent>? RealmAbilityEvents = null);
