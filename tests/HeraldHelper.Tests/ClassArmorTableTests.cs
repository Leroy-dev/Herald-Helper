using HeraldHelper.Desktop;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class ClassArmorTableTests
{
    [Theory]
    [InlineData("Minstrel", DamageVerdict.Resists, DamageVerdict.Neutral, DamageVerdict.Weak)]
    [InlineData("Cleric", DamageVerdict.Resists, DamageVerdict.Neutral, DamageVerdict.Weak)]
    [InlineData("Heretic", DamageVerdict.Resists, DamageVerdict.Neutral, DamageVerdict.Weak)]
    [InlineData("Warrior", DamageVerdict.Neutral, DamageVerdict.Resists, DamageVerdict.Weak)]
    [InlineData("Berserker", DamageVerdict.Neutral, DamageVerdict.Weak, DamageVerdict.Resists)]
    [InlineData("Mauler", DamageVerdict.Neutral, DamageVerdict.Weak, DamageVerdict.Resists)]
    [InlineData("Armsman", DamageVerdict.Weak, DamageVerdict.Neutral, DamageVerdict.Resists)]
    [InlineData("Armswoman", DamageVerdict.Weak, DamageVerdict.Neutral, DamageVerdict.Resists)]
    [InlineData("Hero", DamageVerdict.Resists, DamageVerdict.Weak, DamageVerdict.Neutral)]
    [InlineData("Bard", DamageVerdict.Weak, DamageVerdict.Resists, DamageVerdict.Neutral)]
    public void Lookup_MapsClassToArmorVerdicts(
        string className,
        DamageVerdict thrust,
        DamageVerdict slash,
        DamageVerdict crush)
    {
        Assert.Equal((thrust, slash, crush), ClassArmorTable.Lookup(className));
    }

    [Theory]
    [InlineData("Wizard")]
    [InlineData("Eldritch")]
    [InlineData("Runemaster")]
    [InlineData("Some Mob")]
    [InlineData(null)]
    public void Lookup_ClothAndUnknown_AreNeutralEverywhere(string? className)
    {
        Assert.Equal(
            (DamageVerdict.Neutral, DamageVerdict.Neutral, DamageVerdict.Neutral),
            ClassArmorTable.Lookup(className));
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        Assert.Equal(
            (DamageVerdict.Resists, DamageVerdict.Neutral, DamageVerdict.Weak),
            ClassArmorTable.Lookup("MINSTREL"));
    }

    [Fact]
    public void BuildResistsLines_ColorsWeaknessesGreenAndResistsRed()
    {
        var target = new TargetProfile("Foo", null, "Minstrel", 50, null, null);

        var lines = DesktopOverlayRenderer.BuildResistsLines(target);

        Assert.NotNull(lines);
        Assert.Equal(3, lines!.Count);
        // Minstrel (chain/studded): resists thrust, weak to crush.
        Assert.Equal("Thrust", lines[0].Text);
        Assert.Equal(255, lines[0].Color.R);
        Assert.Equal("Slash", lines[1].Text);
        Assert.Equal(255, lines[1].Color.R);
        Assert.Equal(255, lines[1].Color.G);
        Assert.Equal("Crush", lines[2].Text);
        Assert.Equal(255, lines[2].Color.G);
        Assert.Equal(0, lines[2].Color.R);
    }

    [Fact]
    public void BuildResistsLines_ClothTarget_ReturnsNull()
    {
        var target = new TargetProfile("Foo", null, "Wizard", 50, null, null);
        Assert.Null(DesktopOverlayRenderer.BuildResistsLines(target));
    }

    [Fact]
    public void BuildTargetText_OverlaySettingsFlags_GateFields()
    {
        var target = new TargetProfile("Foo", "Guildname", "Minstrel", 50, "RR5L0", 12);
        var overlay = new OverlaySettings
        {
            ShowGuild = false,
            ShowSoloKills = false
        };

        var text = DesktopOverlayRenderer.BuildTargetText(target, overlay);

        Assert.DoesNotContain("Guildname", text);
        Assert.DoesNotContain("12", text);
        Assert.Contains("Minstrel", text);
        Assert.Contains("RR5L0", text);
    }

    [Fact]
    public void BuildTargetText_AllFieldsOff_ShowsOnlyName()
    {
        var target = new TargetProfile("Foo", "Guildname", "Minstrel", 50, "RR5L0", 12);
        var overlay = new OverlaySettings
        {
            ShowGuild = false,
            ShowClass = false,
            ShowLevel = false,
            ShowRealmRank = false,
            ShowSoloKills = false
        };

        Assert.Equal("Foo", DesktopOverlayRenderer.BuildTargetText(target, overlay));
    }
}
