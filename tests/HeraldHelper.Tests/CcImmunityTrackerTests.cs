using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class CcImmunityTrackerTests
{
    [Fact]
    public void RegisterSuccessfulHit_AddsActiveTimer()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Stun", "s", ControlEffectType.Stun, 5, true);

        tracker.RegisterSuccessfulHit(hit, "Warrior", 0, now);
        var active = tracker.GetActiveTimers(now);

        Assert.Single(active);
    }

    [Fact]
    public void RegisterSuccessfulHit_MeleeStunSkillCode_UsesStunImmunity()
    {
        // Hand-edited profiles use skill_code as the trigger label
        // ('m' = melee line) — immunity math must follow EffectType.
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Slam perfectly", "m", ControlEffectType.Stun, 9, true);

        tracker.RegisterSuccessfulHit(hit, "Cleric", 0, now);
        var active = tracker.GetActiveTimers(now);

        var entry = Assert.Single(active);
        var remaining = entry.RemainingSeconds(now);
        Assert.InRange(remaining, 62, 69); // 60 + ~9s stun path, not the ~63s mezz path
    }

    [Fact]
    public void GetActiveTimers_ExpiresTimerWhenTimePasses()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Stun", "s", ControlEffectType.Stun, 1, true);

        tracker.RegisterSuccessfulHit(hit, "Warrior", 0, now);
        var activeAfterExpiry = tracker.GetActiveTimers(now.AddSeconds(120));

        Assert.Empty(activeAfterExpiry);
    }
}
