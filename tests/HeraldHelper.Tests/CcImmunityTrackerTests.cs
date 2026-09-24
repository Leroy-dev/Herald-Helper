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
    public void RegisterSuccessfulHit_CarriesAbilityIconOntoTimerEntry()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var icon = new IconSpriteRef("eden/spells", 3, 2, 32, 32);
        var hit = new AbilityHit("TargetA", "Slam perfectly", "m", ControlEffectType.Stun, 9, true, Icon: icon);

        tracker.RegisterSuccessfulHit(hit, "Cleric", 0, now);

        Assert.Equal(icon, tracker.GetActiveTimers(now).Single().Icon);
    }

    [Fact]
    public void PreviewImmunitySeconds_MatchesTrackedTimerDuration()
    {
        // The abilities "test line" previews this value — keep it identical
        // to what the runtime tracker actually schedules.
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Slam perfectly", "m", ControlEffectType.Stun, 9, true);

        var preview = CcImmunityTracker.PreviewImmunitySeconds(hit, "Cleric", 25);

        var tracker = new CcImmunityTracker();
        tracker.RegisterSuccessfulHit(hit, "Cleric", 25, now);
        var remaining = tracker.GetActiveTimers(now).Single().RemainingSeconds(now);
        Assert.Equal(preview, remaining);
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

    [Fact]
    public void RetractFreshEntries_RemovesJustCreatedTimer()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("Alice", "Mesmerizing Gaze", "s", ControlEffectType.Mezz, 30, true),
            null, 0, now);

        // Resist line scrolls in a tick later — the timer was wrong.
        tracker.RetractFreshEntries(["Alice"], now.AddSeconds(1.5), TimeSpan.FromSeconds(8));

        Assert.Empty(tracker.GetActiveTimers(now.AddSeconds(2)));
    }

    [Fact]
    public void RetractFreshEntries_KeepsOldTimerAlive()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("Alice", "Stun", "s", ControlEffectType.Stun, 5, true),
            "Warrior", 0, now);

        // A new resisted attempt must not wipe the still-running immunity.
        tracker.RetractFreshEntries(["Alice"], now.AddSeconds(30), TimeSpan.FromSeconds(8));

        Assert.Single(tracker.GetActiveTimers(now.AddSeconds(30)));
    }

    [Fact]
    public void RetractFreshEntries_MatchesAcrossLeadingArticle()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("goborchend wounder", "Charm", "s", ControlEffectType.Mezz, 30, true),
            null, 0, now);

        tracker.RetractFreshEntries(["The goborchend wounder"], now.AddSeconds(1), TimeSpan.FromSeconds(8));

        Assert.Empty(tracker.GetActiveTimers(now.AddSeconds(1)));
    }

    [Fact]
    public void RetractFreshEntries_MatchesAcrossAdapterDashSuffix()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("Level 1 Training Dummy---", "Slam", "s", ControlEffectType.Stun, 9, true),
            null, 0, now);

        tracker.RetractFreshEntries(["Level 1 Training Dummy"], now.AddSeconds(1), TimeSpan.FromSeconds(3));

        Assert.Empty(tracker.GetActiveTimers(now.AddSeconds(1)));
    }

    [Fact]
    public void RegisterSuccessfulHit_NearsightTracksDebuffDurationOnly()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("Alice", "Nearsight", "n", ControlEffectType.Nearsight, 20, true),
            null, 0, now);

        var timer = Assert.Single(tracker.GetActiveTimers(now));
        // No immunity multiplier — nearsight is a debuff, not hard CC.
        Assert.InRange(timer.RemainingSeconds(now), 18, 20);
        Assert.True(timer.RemainingSeconds(now.AddSeconds(19)) > 0);
        Assert.Empty(tracker.GetActiveTimers(now.AddSeconds(21)));
    }
}
