using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Tests;

public sealed class RealmAbilityCooldownTableTests
{
    [Fact]
    public void ParseCooldown_MinutesSeconds()
    {
        Assert.Equal(1200, RealmAbilityCooldownTable.ParseCooldown(
            "Level 1: Can use every: 20:00 min"));
        Assert.Equal(300, RealmAbilityCooldownTable.ParseCooldown(
            "Level 1: Can use every: 05:00 min"));
    }

    [Fact]
    public void ParseCooldown_SecVariant()
    {
        Assert.Equal(90, RealmAbilityCooldownTable.ParseCooldown(
            "Level 1: Can use every: 90 sec"));
    }

    [Fact]
    public void ParseCooldown_Missing_ReturnsNull()
    {
        Assert.Null(RealmAbilityCooldownTable.ParseCooldown("Amount: 4%"));
        Assert.Null(RealmAbilityCooldownTable.ParseCooldown(null));
    }

    [Fact]
    public void Load_NonCatalogShard_UsesServerTable()
    {
        // The CSV carries server-mined GetReUseDelay values joined to display
        // names — Purge = 1800s (the 30-minute RA cooldown) for shards whose
        // charplan catalogs don't cover them.
        var table = RealmAbilityCooldownTable.Load(ShardType.Default, "Hero");

        Assert.NotNull(table);
        Assert.Equal(1800, table["Purge"]);
        Assert.Equal(600, table["Excited Frenzy"]);
    }
}
