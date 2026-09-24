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
    public void RegisterSuccessfulHit_MeleeStyleStun_UsesStyleImmunity()
    {
        // Eden melee/model: a 9s style stun grants 6x9s immunity after the
        // stun ends — 63s total, no spell-resist reduction on styles.
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Slam perfectly", "m", ControlEffectType.Stun, 9, true, IsMeleeStyle: true);

        tracker.RegisterSuccessfulHit(hit, "Cleric", 0, now);
        var active = tracker.GetActiveTimers(now);

        var entry = Assert.Single(active);
        var remaining = entry.RemainingSeconds(now);
        Assert.InRange(remaining, 62, 64); // 9s stun + 54s immunity
    }

    [Fact]
    public void RegisterSuccessfulHit_CastedStun_UsesDurationPlusSixty()
    {
        // Casted CC: immunity is a flat 60s after the effect ends; the casted
        // duration lands at the 0.74 spell-resist fraction first.
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Stunning Bellow", "s", ControlEffectType.Stun, 9, true);

        tracker.RegisterSuccessfulHit(hit, null, 0, now);

        var remaining = Assert.Single(tracker.GetActiveTimers(now)).RemainingSeconds(now);
        Assert.InRange(remaining, 65, 67); // floor(9*0.74)=6 + 60
    }

    [Fact]
    public void RegisterSuccessfulHit_CastedMezz_UsesDurationPlusSixty()
    {
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Mesmerizing Melody", "m", ControlEffectType.Mezz, 30, true);

        tracker.RegisterSuccessfulHit(hit, null, 0, now);

        var remaining = Assert.Single(tracker.GetActiveTimers(now)).RemainingSeconds(now);
        Assert.InRange(remaining, 81, 83); // floor(30*0.74)=22 + 60
    }

    [Fact]
    public void RegisterSuccessfulHit_StyleStun_DetReducesStunAndImmunity()
    {
        // A 20-det target is stunned ~1.8s — immunity scales off the applied
        // stun, not the nominal one.
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        var hit = new AbilityHit("TargetA", "Slam perfectly", "m", ControlEffectType.Stun, 9, true, IsMeleeStyle: true);

        tracker.RegisterSuccessfulHit(hit, "Warrior", 0, now);

        var remaining = Assert.Single(tracker.GetActiveTimers(now)).RemainingSeconds(now);
        Assert.InRange(remaining, 7, 14); // floor(9*0.2)=1 -> 1+6
    }

    [Fact]
    public void RegisterSuccessfulHit_SnareTracksDebuffDurationOnly()
    {
        // Damage+snare spells do not create a root/snare immunity window.
        var tracker = new CcImmunityTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.RegisterSuccessfulHit(
            new AbilityHit("TargetA", "Snare Nuke", "e", ControlEffectType.Snare, 30, true),
            null, 0, now);

        var timer = Assert.Single(tracker.GetActiveTimers(now));
        Assert.InRange(timer.RemainingSeconds(now), 28, 30);
        Assert.Empty(tracker.GetActiveTimers(now.AddSeconds(31)));
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
