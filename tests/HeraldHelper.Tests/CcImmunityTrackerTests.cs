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
