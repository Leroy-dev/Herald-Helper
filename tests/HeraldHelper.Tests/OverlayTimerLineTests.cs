using System.Windows.Media;
using HeraldHelper.Desktop;
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

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, null, Colors.White)!;

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

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, null, Colors.White)!;

        Assert.True(lines[0].Text.Contains("Soon"), "expiring timer should sort first");
        Assert.StartsWith("! ", lines[0].Text);
        // Flash color alternates between white and the effect color with the
        // render tick — both are acceptable, don't pin to the millisecond.
        Assert.DoesNotContain(lines.Skip(1), l => l.Text.StartsWith("! "));
    }
}
