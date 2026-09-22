using HeraldHelper.Application.Services;

namespace HeraldHelper.Tests;

public sealed class ClientStateExtractorTests
{
    private static Dictionary<string, string> Map(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void EmptyOrNull_GivesEmptySnapshot()
    {
        Assert.Same(HeraldHelper.Domain.Models.ClientStateSnapshot.Empty,
            ClientStateExtractor.Extract(null));
        Assert.Same(HeraldHelper.Domain.Models.ClientStateSnapshot.Empty,
            ClientStateExtractor.Extract(new Dictionary<string, string>()));
    }

    [Fact]
    public void GroupMembers_ParseFromIndexedKeys()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("group_number", "3"),
            ("group_name0", "Leroy"), ("group_class0", "Cleric"),
            ("group_health0", "100"), ("group_power0", "87"), ("group_endurance0", "55"),
            ("group_level0", "50"), ("group_zone0", "Emain Macha"),
            ("group_xpos0", "12345.5"), ("group_ypos0", "678"), ("group_zpos0", "0"),
            ("group_0icon0", "4217"), ("group_0icon3", "8899"),
            ("group_name1", "Bowslap"), ("group_class1", "Hunter"), ("group_health1", "64"),
            ("group_name7", "Latejoiner"), ("group_health7", "12")));

        Assert.Equal(3, state.GroupMembers.Count);

        var self = state.GroupMembers[0];
        Assert.Equal("Leroy", self.Name);
        Assert.Equal("Cleric", self.Class);
        Assert.Equal(100, self.HealthPercent);
        Assert.Equal(87, self.PowerPercent);
        Assert.Equal(55, self.EndurancePercent);
        Assert.Equal(50, self.Level);
        Assert.Equal("Emain Macha", self.Zone);
        Assert.Equal(12345.5, self.X);
        Assert.Equal(678, self.Y);
        Assert.Equal(["4217", "8899"], self.BuffIcons);

        Assert.Equal("Bowslap", state.GroupMembers[1].Name);
        Assert.Equal(64, state.GroupMembers[1].HealthPercent);
        Assert.Equal("Latejoiner", state.GroupMembers[2].Name);
    }

    [Fact]
    public void BracketLists_ParseNamesAndStripCounts()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("stats_abil", "[Sprint]\\[Speed of Sound I]\\[Glacial Movement]"),
            ("conc_list", "[Abomination]\\[Abomination|4]")));

        Assert.Equal(["Sprint", "Speed of Sound I", "Glacial Movement"], state.Buffs);
        Assert.Equal(["Abomination", "Abomination"], state.ConcentrationBuffs);
    }

    [Fact]
    public void Pet_ParseVitalsAndFlags()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("mini_pet_title", "Abomination"),
            ("mini_pet_life", "75"),
            ("mini_pet_combat2", "1"),
            ("mini_pet_movement0", "1")));

        var pet = Assert.IsType<HeraldHelper.Domain.Models.PetState>(state.Pet);
        Assert.Equal("Abomination", pet.Title);
        Assert.Equal(75, pet.LifePercent);
        Assert.Equal([2], pet.CombatPetIndices);
        Assert.Equal([0], pet.MovingPetIndices);
    }

    [Fact]
    public void Siege_ParsesWhenPresent_NullOtherwise()
    {
        Assert.Null(ClientStateExtractor.Extract(Map(("foo", "bar"))).Siege);

        var state = ClientStateExtractor.Extract(Map(
            ("siege_timer", "90"), ("siege_moving", "1"), ("siege_hits", "42")));
        var siege = Assert.IsType<HeraldHelper.Domain.Models.SiegeState>(state.Siege);
        Assert.Equal(90, siege.TimerSeconds);
        Assert.True(siege.Moving);
        Assert.Equal(42, siege.Hits);
    }

    [Fact]
    public void Scalars_CombatHeadingRpBpRelicTimers()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("combat_mode", "1"),
            ("compass_heading", "89.5"),
            ("stats_realm_points", "7963728"),
            ("bounty_points", "1548245"),
            ("mino_relic_time_percent", "75"),
            ("release_timer_time", "30"),
            ("timer_time", "90")));

        Assert.True(state.InCombat);
        Assert.Equal(89.5, state.CompassHeading);
        Assert.Equal(7963728L, state.RealmPoints);
        Assert.Equal(1548245L, state.BountyPoints);
        Assert.Equal(75, state.RelicTimePercent);
        Assert.Equal(30, state.ReleaseTimerSeconds);
        Assert.Equal(90, state.TimerSeconds);
    }

    [Fact]
    public void PercentStrings_ParseAsNumbers()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("group_name0", "Leroy"), ("group_health0", "+95%")));

        Assert.Equal(95, state.GroupMembers[0].HealthPercent);
    }

    [Fact]
    public void MalformedValues_DoNotThrow()
    {
        var state = ClientStateExtractor.Extract(Map(
            ("group_name0", "Leroy"), ("group_health0", "garbage"),
            ("stats_realm_points", "notanumber"), ("combat_mode", "x")));

        Assert.Equal("Leroy", state.GroupMembers[0].Name);
        Assert.Null(state.GroupMembers[0].HealthPercent);
        Assert.Null(state.RealmPoints);
        Assert.False(state.InCombat);
    }
}
