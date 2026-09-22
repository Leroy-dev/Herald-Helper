using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Tests;

public sealed class CombatEventParsingTests
{
    private static ChatParseResult Parse(string text) =>
        new AbilitiesChatEventParser([]).Parse(text);

    [Fact]
    public void SelfCc_StunMezzRoot_Recognized()
    {
        var result = Parse("You are stunned! You are mesmerized! You are rooted!");
        Assert.Equal(3, result.SelfCcEvents!.Count);
        Assert.Contains(result.SelfCcEvents, x => x.Effect == ControlEffectType.Stun);
        Assert.Contains(result.SelfCcEvents, x => x.Effect == ControlEffectType.Mezz);
        Assert.Contains(result.SelfCcEvents, x => x.Effect == ControlEffectType.Root);
    }

    [Fact]
    public void SelfCc_RepeatedLine_CountsOccurrences()
    {
        var result = Parse("You are stunned! You are stunned!");
        var stun = Assert.Single(result.SelfCcEvents!);
        Assert.Equal(ControlEffectType.Stun, stun.Effect);
        Assert.Equal(2, stun.OccurrenceOrdinal);
    }

    [Fact]
    public void SelfCc_SleepVariants_MapToMezz()
    {
        var result = Parse("You fall into a deep sleep!");
        Assert.Single(result.SelfCcEvents!, x => x.Effect == ControlEffectType.Mezz);
    }

    [Fact]
    public void Incoming_MeleeHit_ParsesAttackerAndDamage()
    {
        var result = Parse("Grug attacks you! Barbarian hits you for 145 damage!");
        Assert.Equal(2, result.IncomingAttacks!.Count);

        var grug = result.IncomingAttacks.First(x => x.Attacker == "Grug");
        Assert.False(grug.IsCritical);
        Assert.Null(grug.Damage);

        var barb = result.IncomingAttacks.First(x => x.Attacker == "Barbarian");
        Assert.Equal(145, barb.Damage);
        Assert.False(barb.IsCritical);
    }

    [Fact]
    public void Incoming_Critical_Marked()
    {
        var result = Parse("Foo critical hits you for 200 extra damage!");
        var hit = Assert.Single(result.IncomingAttacks!);
        Assert.True(hit.IsCritical);
        Assert.Equal(200, hit.Damage);
    }

    [Fact]
    public void Incoming_Miss_Marked()
    {
        var result = Parse("Foo misses you!");
        var hit = Assert.Single(result.IncomingAttacks!);
        Assert.True(hit.Missed);
        Assert.Equal("Foo", hit.Attacker);
    }

    [Fact]
    public void Incoming_SpellCastOnYou_Marked()
    {
        var result = Parse("Moolish casts a spell on you!");
        var hit = Assert.Single(result.IncomingAttacks!);
        Assert.True(hit.IsSpellCastOnYou);
        Assert.Equal("Moolish", hit.Attacker);
    }

    [Fact]
    public void Incoming_OwnHits_DoNotFire()
    {
        var result = Parse("You hit Foo for 50 damage! You attack Bar!");
        Assert.Empty(result.IncomingAttacks!);
    }

    [Fact]
    public void Kill_ParsesVictimName()
    {
        var result = Parse("You have slain Xmlbeastie!");
        var kill = Assert.Single(result.LifeEvents!);
        Assert.Equal(CombatLifeKind.Kill, kill.Kind);
        Assert.Equal("Xmlbeastie", kill.OtherName);
    }

    [Fact]
    public void Death_WithKiller_ParsesName()
    {
        var result = Parse("You have been killed by Moolish!");
        var death = Assert.Single(result.LifeEvents!);
        Assert.Equal(CombatLifeKind.Death, death.Kind);
        Assert.Equal("Moolish", death.OtherName);
    }

    [Fact]
    public void Death_EnemyKillsYou_ParsesName()
    {
        var result = Parse("Foo kills you!");
        var death = Assert.Single(result.LifeEvents!);
        Assert.Equal(CombatLifeKind.Death, death.Kind);
        Assert.Equal("Foo", death.OtherName);
    }

    [Fact]
    public void Death_Plain_NoName()
    {
        var result = Parse("You die.");
        var death = Assert.Single(result.LifeEvents!);
        Assert.Equal(CombatLifeKind.Death, death.Kind);
        Assert.Null(death.OtherName);
    }

    [Fact]
    public void RealmAbility_Use_ParsesName()
    {
        var result = Parse("You use Purge! You activate Ignore Pain!");
        Assert.Equal(2, result.RealmAbilityEvents!.Count);
        Assert.Contains(result.RealmAbilityEvents, x => x.AbilityName == "Purge");
        Assert.Contains(result.RealmAbilityEvents, x => x.AbilityName == "Ignore Pain");
    }

    [Fact]
    public void MixedChat_ParsesAllEventKinds()
    {
        var result = Parse(
            "You target [Moolish]. Moolish casts a spell on you! " +
            "You are stunned! Moolish hits you for 87 damage! " +
            "You have been killed by Moolish!");

        Assert.NotNull(result.TargetEvent);
        Assert.Single(result.SelfCcEvents!);
        Assert.Equal(2, result.IncomingAttacks!.Count);
        var death = Assert.Single(result.LifeEvents!);
        Assert.Equal(CombatLifeKind.Death, death.Kind);
        Assert.Equal("Moolish", death.OtherName);
    }
}
