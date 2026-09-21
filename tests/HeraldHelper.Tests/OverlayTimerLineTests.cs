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

        var lines = DesktopOverlayRenderer.BuildTimerLines(timers, Colors.White)!;

        Assert.Contains(lines, l => l.Text.Contains("Foo ·Cle"));
        Assert.Contains(lines, l => l.Text.StartsWith("M Bar ") && !l.Text.Contains('·'));
    }
}
