using HeraldHelper.Infrastructure.Casting;

namespace HeraldHelper.Tests;

public sealed class ServerCastSpellCatalogTests
{
    [Fact]
    public void FindBySpellName_ResolvesServerTableRow()
    {
        var catalog = new ServerCastSpellCatalog();

        var info = catalog.FindBySpellName("Compelling Cadence");

        Assert.NotNull(info);
        Assert.Equal("Compelling Cadence", info.SpellName);
        Assert.Equal(3.0, info.CastTimeSeconds);
    }

    [Fact]
    public void FindBySpellName_PrefersRowWithRealRecast()
    {
        var catalog = new ServerCastSpellCatalog();

        var info = catalog.FindBySpellName("Personal Recall Stone");

        Assert.NotNull(info);
        Assert.Equal(1.8, info.RecastSeconds);
    }

    [Fact]
    public void FindBySpellName_StripsCastSuffixNoise()
    {
        var catalog = new ServerCastSpellCatalog();

        var info = catalog.FindBySpellName("Compelling Cadence spell");

        Assert.NotNull(info);
        Assert.Equal("Compelling Cadence", info.SpellName);
    }

    [Fact]
    public void FindBySpellName_UnknownName_ReturnsNull()
    {
        var catalog = new ServerCastSpellCatalog();

        Assert.Null(catalog.FindBySpellName("Definitely Not A Spell"));
    }
}
