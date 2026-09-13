using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Tests;

public sealed class AbilityProfileCatalogTests
{
    [Fact]
    public void EdenMinstrelProfile_UsesPlannerCrowdControlMetadata()
    {
        var profile = AbilityProfileCatalog.GetProfile(ShardType.Eden, "Minstrel");

        var cadence = Assert.Single(profile, x => x.Name == "Commanding Cadence");
        Assert.Equal(29, cadence.DurationSeconds);
        Assert.Equal(ControlEffectType.Mezz, cadence.EffectType);
        Assert.Equal("m", cadence.SkillCode);
    }

    [Fact]
    public void BlackthornArmsmanProfile_UsesAdjustedStyleDuration()
    {
        var profile = AbilityProfileCatalog.GetProfile(ShardType.Blackthorn, "Armsman");

        var slam = Assert.Single(profile, x => x.Name == "Slam");
        Assert.Equal(9, slam.DurationSeconds);
        Assert.Equal(ControlEffectType.Stun, slam.EffectType);
        Assert.Equal("s", slam.SkillCode);
    }

    [Fact]
    public void EdenArmsmanProfile_StylesCarryStyleNamesNotEffectDescriptors()
    {
        var profile = AbilityProfileCatalog.GetProfile(ShardType.Eden, "Armsman");

        // Eden charplan keeps the style name on the skill and the applied-effect
        // text on subSkills — the ability list must show the style name.
        var brutalize = Assert.Single(profile, x => x.Name == "Brutalize");
        Assert.Equal(ControlEffectType.Stun, brutalize.EffectType);
        Assert.DoesNotContain(profile, x => x.Name.StartsWith("Stun,", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Profiles_AreScopedToClassesAvailableOnEachShard()
    {
        var edenClasses = AbilityProfileCatalog.GetClasses(ShardType.Eden);
        var blackthornClasses = AbilityProfileCatalog.GetClasses(ShardType.Blackthorn);

        Assert.Equal(45, edenClasses.Count);
        Assert.Equal(39, blackthornClasses.Count);
        Assert.Contains("Minstrel", edenClasses);
        Assert.Contains("Minstrel", blackthornClasses);
        Assert.DoesNotContain("Mauler", edenClasses);
        Assert.DoesNotContain("Bainshee", blackthornClasses);
    }
}
