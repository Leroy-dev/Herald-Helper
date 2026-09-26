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
    public void SelfCc_ExpireLines_EmitExpiredEvents()
    {
        // Spell-DB Message3 strings — self-expire is reliable because the
        // target is the local player; the banner can clear at real expiry.
        var result = Parse("You recover from the stun. Your vision returns to normal. The bonds holding you break.");

        Assert.Contains(result.SelfCcExpiredEvents!, x => x.Effect == ControlEffectType.Stun);
        Assert.Contains(result.SelfCcExpiredEvents!, x => x.Effect == ControlEffectType.Nearsight);
        Assert.Contains(result.SelfCcExpiredEvents!, x => x.Effect == ControlEffectType.Snare);
        Assert.Empty(result.SelfCcEvents!);
    }

    [Fact]
    public void SelfCc_NewDbStartStrings_Recognized()
    {
        // Message1 variants present in the deployed spell table that were
        // previously uncovered.
        var result = Parse("A flash of light bursts in front of you! Your movement is slowed! You are enveloped by numbing cold!");

        Assert.Contains(result.SelfCcEvents!, x => x.Effect == ControlEffectType.Stun);
        Assert.Equal(2, result.SelfCcEvents!.Count(x => x.Effect == ControlEffectType.Snare));
    }

    [Fact]
    public void Broadcast_ApplyLines_NameAndEffect()
    {
        // Message2 (third-person broadcast) strings from the deployed spell
        // table — these mark a NEARBY player's CC, used for group badges.
        var result = Parse("Bobby is stunned! Walter is entranced. Rocks rise from the ground and trip Merlin! Clyde's feet are frozen to the ground!");

        var applied = result.BroadcastCcEvents!;
        Assert.Contains(applied, x => x.Name == "Bobby" && x.Effect == ControlEffectType.Stun && x.Applied);
        Assert.Contains(applied, x => x.Name == "Walter" && x.Effect == ControlEffectType.Mezz && x.Applied);
        Assert.Contains(applied, x => x.Name == "Merlin" && x.Effect == ControlEffectType.Root && x.Applied);
        Assert.Contains(applied, x => x.Name == "Clyde" && x.Effect == ControlEffectType.Root && x.Applied);
    }

    [Fact]
    public void Broadcast_NameWithDigits_CapturesFullName()
    {
        // Mob/pet names carry digits - 'Level 50 Training Dummy' truncated to
        // 'Training Dummy' broke the current-target match for synthesized
        // timers.
        var result = Parse("Level 50 Training Dummy cannot seem to move!");

        Assert.Contains(result.BroadcastCcEvents ?? [],
            x => x.Name == "Level 50 Training Dummy" &&
                 x.Effect == ControlEffectType.Stun && x.Applied);
    }

    [Fact]
    public void Broadcast_ExpireLines_EmitNotApplied()
    {
        var result = Parse("Bobby recovers from the stun. Walter is no longer entranced. Clyde can move normally again.");

        var expired = result.BroadcastCcEvents!;
        Assert.All(expired, x => Assert.False(x.Applied));
        Assert.Contains(expired, x => x.Name == "Bobby" && x.Effect == ControlEffectType.Stun);
        Assert.Contains(expired, x => x.Name == "Walter" && x.Effect == ControlEffectType.Mezz);
        Assert.Contains(expired, x => x.Name == "Clyde");
    }

    [Fact]
    public void Broadcast_SelfExpireLine_NotABroadcast()
    {
        // "You can move normally again." is the self-expire Message3 — it
        // must NOT produce a broadcast event for a fictitious "You" player.
        var result = Parse("You can move normally again.");

        Assert.Empty(result.BroadcastCcEvents ?? []);
        Assert.Single(result.SelfCcExpiredEvents!, x => x.Effect == ControlEffectType.Snare);
    }

    [Fact]
    public void Broadcast_YouStunned_NotABroadcast()
    {
        var result = Parse("You are stunned!");

        Assert.Empty(result.BroadcastCcEvents ?? []);
        Assert.Single(result.SelfCcEvents!, x => x.Effect == ControlEffectType.Stun);
    }

    [Fact]
    public void Broadcast_HasteDebuffLine_NotCc()
    {
        // MeleeHasteDebuff (attack speed) is not crowd control — no event.
        var result = Parse("Your attacks are being slowed by an invisible force! Bobby's attacks return to normal.");

        Assert.Empty(result.BroadcastCcEvents ?? []);
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
