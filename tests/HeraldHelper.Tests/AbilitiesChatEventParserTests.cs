using HeraldHelper.Application.Models;
using HeraldHelper.Infrastructure.Casting;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Tests;

public sealed class AbilitiesChatEventParserTests
{
    private static readonly AbilityDefinition[] NoAbilities = [];

    [Fact]
    public void Parse_DetectsExactBeginCastingPhrase()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("[22:48:14] You begin casting a Banish Soul spell!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Started, result.CastEvent!.EventType);
        Assert.Equal("Banish Soul", result.CastEvent.SpellName);
    }

    [Fact]
    public void Parse_DetectsExactCastCompletionPhrase()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("[22:48:15] You cast a Banish Soul spell!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Completed, result.CastEvent!.EventType);
        Assert.Equal("Banish Soul", result.CastEvent.SpellName);
    }

    [Fact]
    public void Parse_DetectsSpellcastInterruptPhrase()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("[22:48:19] You move and interrupt your spellcast.");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Interrupted, result.CastEvent!.EventType);
    }

    [Fact]
    public void Parse_IgnoresOtherPlayersGenericCastLines()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("[22:57:34] Zoi casts a spell!");

        Assert.Null(result.CastEvent);
    }

    [Fact]
    public void Parse_NormalizesGenericArticleAndSpellSuffix()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("[22:48:14] You begin casting a Focusing Chant spell!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal("Focusing Chant", result.CastEvent!.SpellName);
    }

    [Fact]
    public void Parse_DetectsBeginPlayingPhrase()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("You begin playing Commanding Cadence!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Started, result.CastEvent!.EventType);
        Assert.Equal("Commanding Cadence", result.CastEvent.SpellName);
    }

    [Fact]
    public void Parse_TargetPlainStopsBeforeFollowingOcrPhrases()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("You target Level 50 Training Dummy You examine Level 50 Training Dummy It is aggressive towards you You begin playing Commanding Cadence!");

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Level 50 Training Dummy", result.TargetEvent!.Name);
    }

    [Fact]
    public void Parse_ReturnsNewestTargetInsteadOfOlderMemberTarget()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("""
            You target [Alice].
            You examine Alice. She is a member of your realm.
            You target [Level 50 Training Dummy].
            You examine Level 50 Training Dummy. It is aggressive towards you.
            """);

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Level 50 Training Dummy", result.TargetEvent!.Name);
        Assert.Equal(TargetMembership.NonMember, result.TargetEvent.Membership);
    }

    [Theory]
    [InlineData("He is friendly towards you.")]
    [InlineData("It is neutral towards you.")]
    public void Parse_ClassifiesFriendlyOrNeutralNpcAsNonMember(string examine)
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse($"""
            You target [Alice].
            You examine Alice. She is a member of your realm.
            You target [Master Vaughn].
            You examine Master Vaughn. {examine}
            """);

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Master Vaughn", result.TargetEvent!.Name);
        Assert.Equal(TargetMembership.NonMember, result.TargetEvent.Membership);
    }

    [Fact]
    public void Parse_DoesNotAssociateOlderMemberLineWithNewestTarget()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("""
            You target [Alice].
            You examine Alice. She is a member of your realm.
            You target [Bob].
            """);

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Bob", result.TargetEvent!.Name);
        Assert.Equal(TargetMembership.Unknown, result.TargetEvent.Membership);
    }

    [Fact]
    public void Parse_ClassifiesNewestMemberWithinItsOwnTargetSegment()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("""
            You target [Level 50 Training Dummy].
            You examine Level 50 Training Dummy. It is aggressive towards you.
            You target [Alice].
            You examine Alice. She is a member of your realm.
            """);

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Alice", result.TargetEvent!.Name);
        Assert.Equal(TargetMembership.Member, result.TargetEvent.Membership);
    }

    [Fact]
    public void Parse_ClassifiesEnemyRealmMemberAsPlayerTarget()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("""
            You target [Warsould].
            You examine Celt. She is a member of an enemy realm!
            """);

        Assert.NotNull(result.TargetEvent);
        Assert.Equal("Warsould", result.TargetEvent!.Name);
        Assert.Equal(TargetMembership.Member, result.TargetEvent.Membership);
    }

    [Fact]
    public void Parse_AssignsOrdinalsToRepeatedTargetAndCastEvents()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("""
            You target [Alice].
            You examine Alice. She is a member of your realm.
            You begin casting a Focusing Chant spell!
            You target [Alice].
            You examine Alice. She is a member of your realm.
            You begin casting a Focusing Chant spell!
            """);

        Assert.Equal([1, 2], result.VisibleTargetEvents!.Select(x => x.OccurrenceOrdinal));
        Assert.Equal([1, 2], result.VisibleCastEvents!.Select(x => x.OccurrenceOrdinal));
        Assert.Equal(2, result.TargetEvent!.OccurrenceOrdinal);
        Assert.Equal(2, result.CastEvent!.OccurrenceOrdinal);
    }

    [Fact]
    public void Parse_AssignsOrdinalsToRepeatedAbilityHits()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 5, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("""
            You target [Alice].
            Slam hits Alice.
            Slam hits Alice.
            """);

        Assert.Equal([1, 2], result.AbilityHits.Select(x => x.OccurrenceOrdinal));
    }

    [Fact]
    public void Parse_UsesAbilityAliasForKnownOcrMisread()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Commanding Cadence", "m", 29, HeraldHelper.Domain.Enums.ControlEffectType.Mezz, ["Commandlng Cadence"])
        ]);

        var result = parser.Parse("You target [Alice]. Commandlng Cadence hits Alice.");

        var hit = Assert.Single(result.AbilityHits);
        Assert.Equal("Commanding Cadence", hit.AbilityName);
    }

    [Fact]
    public void Parse_FuzzyMatchesSingleOcrErrorWithinActiveProfile()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Commanding Cadence", "m", 29, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. Commandlng Cadence hits Alice.");

        Assert.Equal("Commanding Cadence", Assert.Single(result.AbilityHits).AbilityName);
    }

    [Fact]
    public void Parse_UsesFallbackTargetWhenFrameHasNoTargetMention()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 5, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("Slam hits Alice.", fallbackTargetName: "Alice");

        var hit = Assert.Single(result.AbilityHits);
        Assert.Equal("Alice", hit.TargetName);
    }

    [Fact]
    public void Parse_InFrameTargetMentionBeatsFallback()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 5, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Bob]. Slam hits Bob.", fallbackTargetName: "Alice");

        Assert.Equal("Bob", Assert.Single(result.AbilityHits).TargetName);
    }

    [Fact]
    public void Parse_WithoutFallbackStillDropsUntargetedMention()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 5, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("Slam hits Alice.");

        Assert.Empty(result.AbilityHits);
    }

    [Fact]
    public void Parse_PriorCooldownMessageDoesNotSuppressLandedHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. You must wait 4 seconds to use it again. Slam hits Alice.");

        var hit = Assert.Single(result.AbilityHits);
        Assert.True(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_MentionInsideResistLineIsNotLanded()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. Alice resists your Slam!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_DoesNotMatchAbilityInsideAnotherWord()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Root", "r", 20, HeraldHelper.Domain.Enums.ControlEffectType.Root)
        ]);

        var result = parser.Parse("You target [Alice]. Rooted Strength affects Alice.");

        Assert.Empty(result.AbilityHits);
    }

    [Fact]
    public void Parse_DetectsBeginPlayingInsideMergedOcrBlob()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("Level 50 Traini! You examine Level 50 Train aggressive towards you You begin playing Commanding Cadence! The earthshaker casts a spell!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Started, result.CastEvent!.EventType);
        Assert.Equal("Commanding Cadence", result.CastEvent.SpellName);
    }

    [Fact]
    public void Parse_IgnoresFollowUpQueueNoiseAsCastStart()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("You are already casting a spell! You prepare this spell as a follow up!");

        Assert.Null(result.CastEvent);
    }

    [Fact]
    public void Parse_DetectsInterruptInMergedOcrBlob()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("You are already casting a spell! You prepare this spell as a follow up! You move and interrupt your spellcast.");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Interrupted, result.CastEvent!.EventType);
    }

    [Fact]
    public void EdenCastSpellCatalog_ResolvesFocusingChantFromAttributes()
    {
        var catalog = new EdenCastSpellCatalog();

        var result = catalog.FindBySpellName("Focusing Chant");

        Assert.NotNull(result);
        Assert.Equal("Focusing Chant", result!.SpellName);
        Assert.Equal(3, result.CastTimeSeconds);
        Assert.NotNull(result.Icon);
        Assert.Equal("spl_0.png", result.Icon!.SpriteSheet);
        Assert.Equal(9, result.Icon.X);
        Assert.Equal(5, result.Icon.Y);
        Assert.Equal(2, result.Icon.BorderIndex);
        Assert.Equal(0, result.Icon.SpellBadgeIndex);
    }

    [Fact]
    public void EdenCastSpellCatalog_ResolvesCommandingCadenceFromAttributes()
    {
        var catalog = new EdenCastSpellCatalog();

        var result = catalog.FindBySpellName("Commanding Cadence");

        Assert.NotNull(result);
        Assert.Equal("Commanding Cadence", result!.SpellName);
        Assert.Equal(3, result.CastTimeSeconds);
        Assert.NotNull(result.Icon);
        Assert.Equal("spl_0.png", result.Icon!.SpriteSheet);
        Assert.Equal(9, result.Icon.X);
        Assert.Equal(4, result.Icon.Y);
        Assert.Equal(4, result.Icon!.BorderIndex);
        Assert.Equal(0, result.Icon.SpellBadgeIndex);
    }

    [Fact]
    public void EdenCastSpellCatalog_ResolvesMotivationalAnthemWithoutShiftedIconMapping()
    {
        var catalog = new EdenCastSpellCatalog();

        var result = catalog.FindBySpellName("Motivational Anthem");

        Assert.NotNull(result);
        Assert.Equal(3, result!.CastTimeSeconds);
        Assert.NotNull(result.Icon);
        Assert.Equal("spl_100.png", result.Icon!.SpriteSheet);
        Assert.Equal(2, result.Icon.X);
        Assert.Equal(3, result.Icon.Y);
        Assert.Equal(4, result.Icon.BorderIndex);
        Assert.Equal(5, result.Icon.SpellBadgeIndex);
    }

    [Fact]
    public void BlackthornCastSpellCatalog_ResolvesShardSpecificPlannerMetadata()
    {
        var catalog = new BlackthornCastSpellCatalog();

        var result = catalog.FindBySpellName("Motivational Chant");

        Assert.NotNull(result);
        Assert.Equal(3, result!.CastTimeSeconds);
        Assert.NotNull(result.Icon);
        Assert.Equal("blackthorn/spells/spl_100.bmp", result.Icon!.SpriteSheet);
        Assert.Equal(2, result.Icon.X);
        Assert.Equal(3, result.Icon.Y);
        Assert.Equal(32, result.Icon.Width);
        Assert.Equal(32, result.Icon.Height);
    }

    [Fact]
    public void BlackthornCastSpellCatalog_ResolvesConflictingNameByClass()
    {
        var catalog = new BlackthornCastSpellCatalog();

        Assert.Equal(3, catalog.FindBySpellName("Protecting Spirit", "Animist", 50)!.CastTimeSeconds);
        Assert.Equal(4, catalog.FindBySpellName("Protecting Spirit", "Spiritmaster", 50)!.CastTimeSeconds);
    }

    [Fact]
    public void EdenCastSpellCatalog_ResolvesSpellRankByLevelAndFuzzyName()
    {
        var catalog = new EdenCastSpellCatalog();

        Assert.Equal(3, catalog.FindBySpellName("Forest's Servant", "Animist", 0)!.CastTimeSeconds);
        Assert.Equal(5, catalog.FindBySpellName("Forest's Servant", "Animist", 50)!.CastTimeSeconds);
        Assert.NotNull(catalog.FindBySpellName("Motivatlonal Anthem", "Minstrel", 50));
    }

    [Fact]
    public void EdenCastSpellCatalog_PreservesFixedCastTimeMetadata()
    {
        var catalog = new EdenCastSpellCatalog();

        var result = catalog.FindBySpellName("Lesser Bolt of Ruin", "Warlock", 50);

        Assert.NotNull(result);
        Assert.True(result!.IsFixedCastTime);
        Assert.Equal(2, result.CastTimeSeconds);
    }

    [Fact]
    public void CastSpellCatalogs_TreatMinstrelSpeedPulsesAsFixedCastTime()
    {
        var eden = new EdenCastSpellCatalog();
        var blackthorn = new BlackthornCastSpellCatalog();

        Assert.True(eden.FindBySpellName("Motivational Anthem", "Minstrel", 50)!.IsFixedCastTime);
        Assert.True(blackthorn.FindBySpellName("Motivational Anthem", "Minstrel", 50)!.IsFixedCastTime);
    }
}
