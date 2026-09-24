using System.Windows.Media;
using HeraldHelper.Desktop;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class OverlayTimerLineTests
{
    [Fact]
    public void BuildTimerLines_AppendsShortClassTag_WhenKnown()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = new[]
        {
            new CcTimerEntry("Foo", ControlEffectType.Stun, now.AddSeconds(60), TargetClass: "Cleric"),
            new CcTimerEntry("Bar", ControlEffectType.Mezz, now.AddSeconds(60))
        };

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, Colors.White)!;

        Assert.Contains(lines, l => l.Text.Contains("Foo ·Cle"));
        Assert.Contains(lines, l => l.Text.StartsWith("M Bar ") && !l.Text.Contains('·'));
    }

    [Fact]
    public void BuildTimerLines_SortsByRemaining_MarksExpiring()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = new[]
        {
            new CcTimerEntry("Long", ControlEffectType.Stun, now.AddSeconds(50)),
            new CcTimerEntry("Soon", ControlEffectType.Mezz, now.AddSeconds(2)),
            new CcTimerEntry("Mid", ControlEffectType.Root, now.AddSeconds(20))
        };

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, Colors.White)!;

        Assert.True(lines[0].Text.Contains("Soon"), "expiring timer should sort first");
        Assert.StartsWith("! ", lines[0].Text);
        // Flash color alternates between white and the effect color with the
        // render tick — both are acceptable, don't pin to the millisecond.
        Assert.DoesNotContain(lines.Skip(1), l => l.Text.StartsWith("! "));
    }

    [Fact]
    public void BuildTimerLines_ReadinessListsOpenCategoriesForCurrentTarget()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = new[]
        {
            new CcTimerEntry("Xmlbeastie", ControlEffectType.Mezz, now.AddSeconds(40)),
            new CcTimerEntry("Otherguy", ControlEffectType.Stun, now.AddSeconds(50))
        };

        var lines = DesktopOverlayRenderer.BuildTimerLines(
            timers, Colors.White, currentTargetName: "Xmlbeastie")!;

        var ready = Assert.Single(lines, l => l.Text.StartsWith("READY:"));
        Assert.Contains("Stun", ready.Text);
        Assert.Contains("Root", ready.Text);
        Assert.DoesNotContain("Mezz", ready.Text);
    }

    [Fact]
    public void BuildTimerLines_ReadinessNormalizesArticlePrefix()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = new[]
        {
            new CcTimerEntry("the goborchend wounder", ControlEffectType.Stun, now.AddSeconds(60)),
            new CcTimerEntry("the goborchend wounder", ControlEffectType.Mezz, now.AddSeconds(60)),
            new CcTimerEntry("the goborchend wounder", ControlEffectType.Root, now.AddSeconds(60))
        };

        var lines = DesktopOverlayRenderer.BuildTimerLines(
            timers, Colors.White, currentTargetName: "goborchend wounder")!;

        var ready = Assert.Single(lines, l => l.Text.StartsWith("READY:"));
        Assert.Equal("READY: none", ready.Text);
    }

    [Fact]
    public void BuildTimerLines_ReadinessAbsentWithoutTarget()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = new[] { new CcTimerEntry("Foo", ControlEffectType.Stun, now.AddSeconds(60)) };

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, Colors.White)!;

        Assert.DoesNotContain(lines, l => l.Text.StartsWith("READY:"));
    }

    [Fact]
    public void BuildResistsLines_AppendsVitalsAndOwnResists()
    {
        var now = DateTimeOffset.UtcNow;
        var target = new TargetProfile("Xmlbeastie", null, "Armsman", 50, null, null);
        var state = new ClientStateSnapshot(
            [], [], [], null, null, false, null, null, null, null, null, null,
            Vitals: new PlayerVitals(82, 60, null,
                new Dictionary<string, int> { ["thrust"] = 15, ["slash"] = 50, ["heat"] = -20 }),
            TargetHealthPercent: 44);

        var lines = DesktopOverlayRenderer.BuildResistsLines(target, state)!;

        Assert.Contains(lines, l => l.Text.Contains("HP 82%") && l.Text.Contains("PW 60%"));
        Assert.Contains(lines, l => l.Text.Contains("THR +15%") && l.Text.Contains("SLA +50%"));
        Assert.Contains(lines, l => l.Text.Contains("HEA -20%"));
        Assert.Contains(lines, l => l.Text == "Thrust"); // target verdict still present
    }

    [Fact]
    public void BuildTargetText_AppendsHealthPercent()
    {
        var target = new TargetProfile("Xmlbeastie", null, "Armsman", 50, null, null);
        var overlay = new OverlaySettings();

        var text = DesktopOverlayRenderer.BuildTargetText(target, overlay, targetHealthPercent: 67);

        Assert.Contains("67%", text);
    }
}
