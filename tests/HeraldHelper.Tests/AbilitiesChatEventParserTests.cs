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
    public void Parse_BeginCastingMentionDoesNotCreateHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Gaze", "s", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You begin casting a Mesmerizing Gaze spell!");

        Assert.Empty(result.AbilityHits);
        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Started, result.CastEvent!.EventType);
    }

    [Fact]
    public void Parse_CompletedCastCreatesHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Gaze", "s", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You cast a Mesmerizing Gaze spell!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.True(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_CastResistedByTargetIsNotLandedAndEmitsNegation()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Gaze", "s", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You cast a Mesmerizing Gaze spell! Alice resists the effect! (34.0%)");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
        var negation = Assert.Single(result.NegationEvents!);
        Assert.Equal(NegationKind.Resisted, negation.Kind);
        Assert.Equal("Alice", negation.TargetName);
    }

    [Fact]
    public void Parse_ImmuneTargetEmitsNegation()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Gaze", "s", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You cast a Mesmerizing Gaze spell! Your target is immune to this effect!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
        var negation = Assert.Single(result.NegationEvents!);
        Assert.Equal(NegationKind.Immune, negation.Kind);
        Assert.Null(negation.TargetName);
    }

    [Fact]
    public void Parse_ChargingTargetIsNotLanded()
    {
        // AbstractCCSpellHandler: charge/sprint targets are CC-immune —
        // "Your target is moving too fast for this spell to have any
        // effect!" — the timer must not start.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Gaze", "s", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You cast a Mesmerizing Gaze spell! Your target is moving too fast for this spell to have any effect!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
        Assert.Contains(result.NegationEvents!, n => n.Kind == NegationKind.Immune);
    }

    [Fact]
    public void Parse_SnareImmuneNamedTargetIsNotLanded()
    {
        // SpeedDecreaseSpellHandler emits "{name} is moving to fast for
        // this spell to have any effect!" (server typo "to fast" kept) —
        // a named failed application so the wrong timer can't persist.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Root", "s", 60, HeraldHelper.Domain.Enums.ControlEffectType.Root)
        ]);

        var result = parser.Parse("You target [Alice]. You cast a Root spell! Alice is moving to fast for this spell to have any effect!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
        var negation = Assert.Single(
            result.NegationEvents!, n => n.Kind == NegationKind.FailedApplication);
        Assert.Equal("Alice", negation.TargetName);
    }

    [Fact]
    public void Parse_StylePrepareDoesNotCreateHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. You prepare to perform a Slam!");

        Assert.Empty(result.AbilityHits);
    }

    [Fact]
    public void Parse_StylePerformCreatesLandedHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. You attack Alice with your mace and hit for 172 (-61) damage! You perform your Slam perfectly! (+67, GR: 0.573)");

        var hit = Assert.Single(result.AbilityHits);
        Assert.Equal("Slam", hit.AbilityName);
        Assert.True(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_StyleFailProducesNoHitAndEmitsNegation()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. You fail to execute your Slam perfectly!");

        Assert.Empty(result.AbilityHits);
        var negation = Assert.Single(result.NegationEvents!);
        Assert.Equal(NegationKind.StyleFailed, negation.Kind);
    }

    [Fact]
    public void Parse_SwingDeflectedEmitsNegation()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("You target [Alice]. Alice evades your attack!");

        var negation = Assert.Single(result.NegationEvents!);
        Assert.Equal(NegationKind.SwingFailed, negation.Kind);
        Assert.Equal("Alice", negation.TargetName);
    }

    [Fact]
    public void Parse_SpellCancelledIsInterrupted()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("Your spell is cancelled!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Interrupted, result.CastEvent!.EventType);
    }

    [Fact]
    public void Parse_AttackInterruptIsInterrupted()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("The goborchend wounder is attacking you and your spellcast is interrupted!");

        Assert.NotNull(result.CastEvent);
        Assert.Equal(CastEventType.Interrupted, result.CastEvent!.EventType);
    }

    [Fact]
    public void Parse_CancelYourEffectIsNotInterrupted()
    {
        var parser = new AbilitiesChatEventParser(NoAbilities);

        var result = parser.Parse("You cancel your effect.");

        Assert.True(result.CastEvent is null || result.CastEvent.EventType != CastEventType.Interrupted);
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

    [Fact]
    public void Parse_BeginPlayingSongCreatesLandedHit()
    {
        // Minstrel mez is a song — "You begin playing X!" IS the
        // application; the client prints no "You cast" line for songs.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Mesmerizing Melody", "m", 30, HeraldHelper.Domain.Enums.ControlEffectType.Mezz)
        ]);

        var result = parser.Parse("You target [Alice]. You begin playing Mesmerizing Melody!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.True(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_CantHaveEffectAgainSuppressesCastHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Stunning Bellow", "s", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse(
            "You target [Level 50 Training Dummy]. " +
            "You cast a Stunning Bellow spell! " +
            "Level 50 Training Dummy can't have that effect again yet!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
        Assert.Contains(result.NegationEvents!, x => x.Kind == NegationKind.FailedApplication);
    }

    [Fact]
    public void Parse_AlreadyHasThisEffectSuppressesStyleHit()
    {
        // Live log: "You perform your Slam perfectly!" + "X already has
        // this effect!" — the swing landed but the stun was rejected.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse(
            "You target [Level 1 Training Dummy]. " +
            "You perform your Slam perfectly! (+97, Growth Rate: 0.78) " +
            "You attack Level 1 Training Dummy with your Brimstone Shield of Anarchy and hit for 297 damage! " +
            "Level 1 Training Dummy already has this effect!");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_YourTargetAlreadyHasThatEffectSuppressesHit()
    {
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse(
            "You target [Alice]. You perform your Slam perfectly! " +
            "Your target already has that effect! Wait until it expires. Spell failed.");

        var hit = Assert.Single(result.AbilityHits);
        Assert.False(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_EnterCombatModeTargetResolvesStyleHit()
    {
        // Melee swings print "You enter combat mode and target [X]" —
        // without it the parser fell back to a stale adapter target and
        // attributed the CC to the wrong name.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse(
            "You enter combat mode and target [Level 50 Training Dummy]. " +
            "You perform your Slam perfectly! (+29, Growth Rate: 0.78)",
            fallbackTargetName: "Camelot Cleric");

        var hit = Assert.Single(result.AbilityHits);
        Assert.Equal("Level 50 Training Dummy", hit.TargetName);
        Assert.True(hit.LandedSuccessfully);
    }

    [Fact]
    public void Parse_FallbackTargetTrailingDashesAreStripped()
    {
        // The adapter layer appends "---" to non-player target names.
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "s", 9, HeraldHelper.Domain.Enums.ControlEffectType.Stun)
        ]);

        var result = parser.Parse("Slam hits the dummy.", fallbackTargetName: "Level 1 Training Dummy---");

        var hit = Assert.Single(result.AbilityHits);
        Assert.Equal("Level 1 Training Dummy", hit.TargetName);
    }

    [Fact]
    public void Parse_NearsightEffectCodeResolves()
    {
        Assert.Equal(
            HeraldHelper.Domain.Enums.ControlEffectType.Nearsight,
            AbilitiesChatEventParser.ParseEffectTypeCode("n"));
    }
}
