using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Application.Services;
using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Tests;

public sealed class GameLoopOrchestratorTests
{
    [Fact]
    public async Task TickAsync_ResolvesMemberTargetAndRendersResolvedProfile()
    {
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            new TargetEvent("TargetA", IsMemberTarget: true),
            []));
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("TargetA", "Guild", "Hero", 50, "RR5L0", 12));
        var heraldFactory = new FakeHeraldClientFactory(heraldClient);
        var castCatalog = new FakeCastSpellCatalog();
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: DateTimeOffset.UtcNow,
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, heraldClient.CallCount);
        Assert.NotNull(overlay.LastSnapshot);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("Hero", overlay.LastSnapshot.Target?.Class);
        Assert.Equal("ignored", overlay.LastSnapshot.RawOcrText);
    }

    [Fact]
    public async Task TickAsync_DoesNotResolveNonMemberTarget()
    {
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            new TargetEvent("NotAMember", IsMemberTarget: false),
            []));
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("NotAMember", "Guild", "Hero", 50, "RR5L0", 12));
        var heraldFactory = new FakeHeraldClientFactory(heraldClient);
        var castCatalog = new FakeCastSpellCatalog();
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: DateTimeOffset.UtcNow,
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, heraldClient.CallCount);
        Assert.NotNull(overlay.LastSnapshot);
        Assert.Null(overlay.LastSnapshot!.Target);
    }

    [Fact]
    public async Task TickAsync_AbilityLineWithoutTargetMention_ResolvesToCurrentTarget()
    {
        // Incremental sources (scrollback/chatlog) emit only new lines — the
        // "you target" line is long gone by the time the style fires. The hit
        // must resolve against the tracked current target.
        var now = DateTimeOffset.UtcNow;
        var capture = new SequenceChatCaptureService(
        [
            "You target [Alice].",
            "You perform the Slam perfectly!"
        ]);
        var parser = new AbilitiesChatEventParser(
        [
            new AbilityDefinition("Slam", "m", 9, ControlEffectType.Stun)
        ]);
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            tracker,
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddMilliseconds(400), CancellationToken.None);

        var hit = Assert.Single(tracker.RegisteredHits);
        Assert.Equal("Alice", hit.TargetName);
        Assert.Equal("Slam", hit.AbilityName);
        Assert.Equal(ControlEffectType.Stun, hit.EffectType);
    }

    [Fact]
    public async Task TickAsync_KeepsPreviousProfileWhenNewestTargetIsNonMember()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []),
            new ChatParseResult(new TargetEvent("Training Dummy", TargetMembership.NonMember), [])
        ]);
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("TargetA", "Guild", "Hero", 50, "RR5L0", 12));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            now,
            CancellationToken.None);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            now.AddSeconds(1),
            CancellationToken.None);

        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal(1, heraldClient.CallCount);
    }

    [Fact]
    public async Task TickAsync_AdapterTargetWithoutChatLineStillResolves()
    {
        // Loop started mid-fight: no "you target" line ever arrives, but the
        // live adapter registry knows the selection — herald still resolves.
        var adapters = new FakeAdapterValueSource
        {
            LatestAdapterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary_target"] = "AdapterTarget"
            }
        };
        var capture = new FakeChatCaptureService("just some ambient text");
        var parser = new FakeChatEventParser(new ChatParseResult(null, []));
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("AdapterTarget", "Guild", "Hero", 50, "RR5L0", 3));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay,
            adapterValueSource: adapters);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, heraldClient.CallCount);
        Assert.Equal("AdapterTarget", overlay.LastSnapshot!.Target?.Name);
    }

    [Fact]
    public async Task TickAsync_UnknownMembershipStillUsesHeraldForVerification()
    {
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            new TargetEvent("TargetA", TargetMembership.Unknown),
            []));
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("TargetA", "Guild", "Hero", 50, "RR5L0", 12));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(1, heraldClient.CallCount);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
    }

    [Fact]
    public async Task TickAsync_KeepsLastPlayerWhenNewPlayerLookupFailsAndRetriesLater()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []),
            new ChatParseResult(new TargetEvent("TargetB", TargetMembership.Unknown), []),
            new ChatParseResult(new TargetEvent("TargetB", TargetMembership.Unknown), []),
            new ChatParseResult(new TargetEvent("TargetB", TargetMembership.Unknown), [])
        ]);
        var heraldClient = new NamedHeraldClient(new Dictionary<string, TargetProfile?>
        {
            ["TargetA"] = new TargetProfile("TargetA", "Guild", "Hero", 50, "RR5L0", 12),
            ["TargetB"] = null
        });
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1), CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(5), CancellationToken.None);

        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal(2, heraldClient.CallCount);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(6), CancellationToken.None);

        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal(3, heraldClient.CallCount);
    }

    [Fact]
    public async Task TickAsync_DoesNotWaitForHeraldAndIgnoresLateOldTargetResponse()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []),
            new ChatParseResult(new TargetEvent("TargetB", TargetMembership.Member), []),
            new ChatParseResult(new TargetEvent("TargetB", TargetMembership.Member), [])
        ]);
        var heraldClient = new ControlledHeraldClient();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            now,
            CancellationToken.None);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Null(overlay.LastSnapshot.Target!.Class);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            now.AddSeconds(1),
            CancellationToken.None);
        Assert.Equal("TargetB", overlay.LastSnapshot!.Target?.Name);
        Assert.Null(overlay.LastSnapshot.Target!.Class);

        heraldClient.Complete("TargetA", new TargetProfile("TargetA", "Old", "Hero", 50, "RR1L0", 0));
        heraldClient.Complete("TargetB", new TargetProfile("TargetB", "New", "Hero", 50, "RR2L0", 0));

        for (var attempt = 0; attempt < 10 && overlay.LastSnapshot!.Target?.Guild is null; attempt++)
        {
            await Task.Delay(10);
            await orchestrator.TickAsync(
                new ScreenRegion(0, 0, 100, 30),
                ShardType.Eden,
                10,
                now.AddSeconds(2 + attempt),
                CancellationToken.None);
        }

        Assert.Equal("TargetB", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("New", overlay.LastSnapshot.Target?.Guild);
    }

    [Fact]
    public async Task TickAsync_DisplaysCachedTargetBeforeHeraldLookupCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var cache = new MemoryTargetProfileCache();
        cache.Save(ShardType.Blackthorn, new TargetProfile("Teagan", "Cached Guild", "Minstrel", 50, "RR5L0", 42));
        var heraldClient = new ControlledHeraldClient();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            new FakeChatEventParser(new ChatParseResult(new TargetEvent("Teagan", TargetMembership.Member), [])),
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay,
            targetProfileCache: cache);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Blackthorn, 10, now, CancellationToken.None);

        Assert.Equal("Teagan", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("Cached Guild", overlay.LastSnapshot.Target?.Guild);
        Assert.Equal("Minstrel", overlay.LastSnapshot.Target?.Class);
    }

    [Fact]
    public async Task TickAsync_DoesNotRevertToOlderVisibleTargetAfterOcrMiss()
    {
        var now = DateTimeOffset.UtcNow;
        var alice = new TargetEvent("Alice", TargetMembership.Member, 1);
        var bob = new TargetEvent("Bob", TargetMembership.Member, 1);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(alice, [], VisibleTargetEvents: [alice]),
            new ChatParseResult(bob, [], VisibleTargetEvents: [alice, bob]),
            new ChatParseResult(alice, [], VisibleTargetEvents: [alice])
        ]);
        var heraldClient = new NamedHeraldClient(new Dictionary<string, TargetProfile?>
        {
            ["Alice"] = new TargetProfile("Alice", "Old", "Hero", 50, "RR1L0", 0),
            ["Bob"] = new TargetProfile("Bob", "New", "Hero", 50, "RR2L0", 0)
        });
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1), CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(2), CancellationToken.None);

        Assert.Equal("Bob", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal(2, heraldClient.CallCount);
    }

    [Fact]
    public async Task TickAsync_SkipsDuplicateHitsWithinSameTick()
    {
        var hit = new AbilityHit("TargetA", "Slam", "s", ControlEffectType.Stun, 5, true);
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(null, [hit, hit]));
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog();
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: DateTimeOffset.UtcNow,
            cancellationToken: CancellationToken.None);

        Assert.Single(tracker.RegisteredHits);
        Assert.Equal("Slam", tracker.RegisteredHits[0].AbilityName);
    }

    [Fact]
    public async Task TickAsync_RegistersOnlyNewAbilityOccurrencesAcrossVisibleFrames()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new AbilityHit("TargetA", "Slam", "s", ControlEffectType.Stun, 5, true, 1);
        var second = first with { OccurrenceOrdinal = 2 };
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [first]),
            new ChatParseResult(null, [first]),
            new ChatParseResult(null, [first, second])
        ]);
        var tracker = new RecordingCcImmunityTracker();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            tracker,
            new RecordingOverlayRenderer());

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1), CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(2), CancellationToken.None);

        Assert.Equal(2, tracker.RegisteredHits.Count);
        Assert.Equal([1, 2], tracker.RegisteredHits.Select(x => x.OccurrenceOrdinal));
    }

    [Fact]
    public async Task TickAsync_DoesNotLookupUnchangedCompleteTargetAgainImmediately()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            new TargetEvent("TargetA", IsMemberTarget: true),
            []));
        var heraldClient = new FakeHeraldClient(
            new TargetProfile("TargetA", "Guild", "Hero", 50, "RR5L0", 12));
        var heraldFactory = new FakeHeraldClientFactory(heraldClient);
        var castCatalog = new FakeCastSpellCatalog();
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);
        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now.AddSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, heraldClient.CallCount);
    }

    [Fact]
    public async Task TickAsync_StartsCastBarWhenCastMetadataExists()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            null,
            [],
            new CastEvent(CastEventType.Started, "Fireball")));
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null));
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot?.ActiveCast);
        Assert.Equal("Fireball", overlay.LastSnapshot!.ActiveCast!.SpellName);
        Assert.Equal(3, overlay.LastSnapshot.ActiveCast.TotalSeconds);
    }

    [Fact]
    public async Task TickAsync_NormalizesGenericSpellPhrasingBeforeLookup()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            null,
            [],
            new CastEvent(CastEventType.Started, "a Focusing Chant spell")));
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Focusing Chant", 3, null));
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot?.ActiveCast);
        Assert.Equal("Focusing Chant", overlay.LastSnapshot!.ActiveCast!.SpellName);
    }

    [Fact]
    public async Task TickAsync_DoesNotRestartSameActiveCastAcrossTicks()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            null,
            [],
            new CastEvent(CastEventType.Started, "Fireball")));
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null));
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);

        var firstStart = overlay.LastSnapshot!.ActiveCast!.StartedAtUtc;

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now.AddSeconds(1),
            cancellationToken: CancellationToken.None);

        Assert.Equal(firstStart, overlay.LastSnapshot!.ActiveCast!.StartedAtUtc);
    }

    [Fact]
    public async Task TickAsync_DoesNotRestartExpiredCastFromSameVisibleChatLine()
    {
        var now = DateTimeOffset.UtcNow;
        var castEvent = new CastEvent(CastEventType.Started, "Fireball", 1);
        var parser = new FakeChatEventParser(new ChatParseResult(
            null,
            [],
            castEvent,
            VisibleCastEvents: [castEvent]));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null)),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(4), CancellationToken.None);

        Assert.Null(overlay.LastSnapshot!.ActiveCast);
    }

    [Fact]
    public async Task TickAsync_StartsRepeatedCastWhenNewOccurrenceAppears()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new CastEvent(CastEventType.Started, "Fireball", 1);
        var second = new CastEvent(CastEventType.Started, "Fireball", 2);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], first, VisibleCastEvents: [first]),
            new ChatParseResult(null, [], first, VisibleCastEvents: [first]),
            new ChatParseResult(null, [], second, VisibleCastEvents: [first, second])
        ]);
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null)),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(4), CancellationToken.None);
        Assert.Null(overlay.LastSnapshot!.ActiveCast);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(5), CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot!.ActiveCast);
        Assert.Equal(now.AddSeconds(5), overlay.LastSnapshot.ActiveCast!.StartedAtUtc);
    }

    [Fact]
    public async Task TickAsync_StartsRepeatedCastAfterTheOldLineLeavesTheOcrViewport()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new CastEvent(CastEventType.Started, "Fireball", 1);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], start, VisibleCastEvents: [start]),
            new ChatParseResult(null, [], null, VisibleCastEvents: []),
            new ChatParseResult(null, [], start, VisibleCastEvents: [start])
        ]);
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null)),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(4), CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(5), CancellationToken.None);

        Assert.Equal(now.AddSeconds(5), overlay.LastSnapshot!.ActiveCast!.StartedAtUtc);
    }

    [Fact]
    public async Task TickAsync_ClearsCastBarWhenInterrupted()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null));
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], new CastEvent(CastEventType.Started, "Fireball")),
            new ChatParseResult(null, [], new CastEvent(CastEventType.Interrupted, null))
        ]);
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now.AddSeconds(1),
            cancellationToken: CancellationToken.None);

        Assert.Null(overlay.LastSnapshot!.ActiveCast);
    }

    [Fact]
    public async Task TickAsync_ClearsCastBarWhenCastCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var heraldFactory = new FakeHeraldClientFactory(new FakeHeraldClient(null));
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Fireball", 3, null));
        var tracker = new RecordingCcImmunityTracker();
        var overlay = new RecordingOverlayRenderer();
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], new CastEvent(CastEventType.Started, "Fireball")),
            new ChatParseResult(null, [], new CastEvent(CastEventType.Completed, "Fireball"))
        ]);
        var orchestrator = new GameLoopOrchestrator(capture, parser, castCatalog, heraldFactory, tracker, overlay);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now,
            cancellationToken: CancellationToken.None);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 10,
            nowUtc: now.AddSeconds(1),
            cancellationToken: CancellationToken.None);

        Assert.Null(overlay.LastSnapshot!.ActiveCast);
    }

    [Fact]
    public async Task TickAsync_KeepsFallbackChatAndAcceptsStableStatsAfterTwoFrames()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new RegionAwareCaptureService();
        var savedStats = new List<CharacterStatsSnapshot>();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            new FakeChatEventParser(new ChatParseResult(null, [])),
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            new RecordingOverlayRenderer(),
            ocrWatchRegions: [new OcrWatchRegion("character-stats", "Character Stats", new ScreenRegion(20, 30, 140, 110))],
            activeCharacterName: "Rooy",
            saveCharacterStats: savedStats.Add);

        await orchestrator.TickAsync(new ScreenRegion(1, 2, 300, 100), ShardType.Eden, 10, now, CancellationToken.None);
        Assert.Empty(savedStats);
        await orchestrator.TickAsync(new ScreenRegion(1, 2, 300, 100), ShardType.Eden, 10, now.AddSeconds(1), CancellationToken.None);

        Assert.Equal(4, capture.Regions.Count);
        Assert.Equal([1, 20, 1, 20], capture.Regions.Select(x => x.X));
        Assert.Equal(2, capture.BeginBatchCount);
        Assert.Equal(2, capture.CompleteBatchCount);
        Assert.Single(savedStats);
        Assert.Equal(188, savedStats[0].Dexterity);
    }

    [Fact]
    public async Task TickAsync_PropagatesCancellationAndCompletesCaptureBatch()
    {
        var capture = new CancelingCaptureService();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            new FakeChatEventParser(new ChatParseResult(null, [])),
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            new RecordingOverlayRenderer());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.TickAsync(
            new ScreenRegion(1, 2, 300, 100),
            ShardType.Eden,
            10,
            DateTimeOffset.UtcNow,
            cancellation.Token));

        Assert.Equal(1, capture.BeginBatchCount);
        Assert.Equal(1, capture.CompleteBatchCount);
    }

    [Fact]
    public async Task TickAsync_InstantlyDisplaysPendingTargetNameBeforeHeraldCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(
            new TargetEvent("TargetA", TargetMembership.Member),
            []));
        var heraldClient = new ControlledHeraldClient();
        var overlay = new RecordingOverlayRenderer();
        var diagnostics = new RecordingResponseDiagnostics();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay,
            diagnostics: diagnostics);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            10,
            now,
            CancellationToken.None);

        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.True(overlay.LastSnapshot.Target!.IsLoading);
        Assert.Equal(
            "TargetA\nLoading...",
            DesktopOverlayRenderer.BuildTargetText(
                overlay.LastSnapshot.Target,
                new Dictionary<string, string>()));

        Assert.Contains(diagnostics.Lines, l => l.Contains("[Timing] capture Chat:", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Lines, l => l.Contains("[Timing] parse:", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Lines, l => l.Contains("[Timing] render:", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Lines, l => l.Contains("[Timing] tick:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TickAsync_CachedTargetIsNotLoadingAndHasFullDetails()
    {
        var now = DateTimeOffset.UtcNow;
        var cache = new MemoryTargetProfileCache();
        cache.Save(ShardType.Blackthorn, new TargetProfile("Teagan", "Cached Guild", "Minstrel", 50, "RR5L0", 42));
        var heraldClient = new ControlledHeraldClient();
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            new FakeChatEventParser(new ChatParseResult(new TargetEvent("Teagan", TargetMembership.Member), [])),
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay,
            targetProfileCache: cache);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Blackthorn, 10, now, CancellationToken.None);

        Assert.Equal("Teagan", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("Cached Guild", overlay.LastSnapshot.Target?.Guild);
        Assert.Equal("Minstrel", overlay.LastSnapshot.Target?.Class);
        Assert.False(overlay.LastSnapshot.Target!.IsLoading);
        Assert.DoesNotContain("Loading", DesktopOverlayRenderer.BuildTargetText(overlay.LastSnapshot.Target, new Dictionary<string, string>()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TickAsync_NonMemberTargetKeepsPreviousPlayerVisible()
    {
        var now = DateTimeOffset.UtcNow;
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []),
            new ChatParseResult(new TargetEvent("Training Dummy", TargetMembership.NonMember), [])
        ]);
        var heraldClient = new NamedHeraldClient(new Dictionary<string, TargetProfile?>
        {
            ["TargetA"] = new TargetProfile("TargetA", "Guild", "Hero", 50, "RR1L0", 0)
        });
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(heraldClient),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.False(overlay.LastSnapshot.Target!.IsLoading);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1), CancellationToken.None);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("Hero", overlay.LastSnapshot.Target?.Class);
    }

    [Fact]
    public async Task TickAsync_FixedCastBarUsesThreeSecondDuration()
    {
        var now = DateTimeOffset.UtcNow;
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], new CastEvent(CastEventType.Started, "MinstrelSpeed", 1))
        ]);
        var castCatalog = new FakeCastSpellCatalog(
            new CastSpellInfo("MinstrelSpeed", 3.0, null, IsFixedCastTime: true));
        var overlay = new RecordingOverlayRenderer();
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25, 10, now);

        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            castCatalog,
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay,
            loadCharacterStats: () => stats,
            dynamicCastSpeed: true);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot!.ActiveCast);
        Assert.Equal("MinstrelSpeed", overlay.LastSnapshot.ActiveCast!.SpellName);
        Assert.Equal(3.0, overlay.LastSnapshot.ActiveCast.TotalSeconds);
    }

    [Fact]
    public async Task TickAsync_AdjustableCastBarClampsAtTwoSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], new CastEvent(CastEventType.Started, "FireBolt", 1))
        ]);
        var castCatalog = new FakeCastSpellCatalog(
            new CastSpellInfo("FireBolt", 2.5, null));
        var overlay = new RecordingOverlayRenderer();
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25, 10, now);

        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            castCatalog,
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay,
            loadCharacterStats: () => stats,
            dynamicCastSpeed: true);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot!.ActiveCast);
        Assert.Equal("FireBolt", overlay.LastSnapshot.ActiveCast!.SpellName);
        Assert.Equal(2.0, overlay.LastSnapshot.ActiveCast.TotalSeconds);
    }

    [Fact]
    public async Task TickAsync_ContinuouslyVisibleStartLineStartsOneBar()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new CastEvent(CastEventType.Started, "Bolt", 1);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], start),
            new ChatParseResult(null, [], start),
            new ChatParseResult(null, [], start)
        ]);
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Bolt", 2.5, null));
        var overlay = new RecordingOverlayRenderer();

        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            castCatalog,
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        var firstStartedAt = overlay.LastSnapshot!.ActiveCast!.StartedAtUtc;
        Assert.Equal("Bolt", overlay.LastSnapshot.ActiveCast.SpellName);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(0.5), CancellationToken.None);
        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1.0), CancellationToken.None);

        Assert.Equal(firstStartedAt, overlay.LastSnapshot!.ActiveCast!.StartedAtUtc);
    }

    [Fact]
    public async Task TickAsync_InterruptionPreventsOldStartLineFromRestarting()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new CastEvent(CastEventType.Started, "Bolt", 1);
        var interrupt = new CastEvent(CastEventType.Interrupted, "Bolt", 1);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], start),
            new ChatParseResult(null, [], VisibleCastEvents: [start, interrupt]),
            new ChatParseResult(null, [], start)
        ]);
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Bolt", 2.5, null));
        var overlay = new RecordingOverlayRenderer();

        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            castCatalog,
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        Assert.NotNull(overlay.LastSnapshot!.ActiveCast);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(0.5), CancellationToken.None);
        Assert.Null(overlay.LastSnapshot!.ActiveCast);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1.0), CancellationToken.None);
        Assert.Null(overlay.LastSnapshot!.ActiveCast);
    }

    [Fact]
    public async Task TickAsync_GenuineRepeatedCastStartsSecondBarAfterCompletion()
    {
        var now = DateTimeOffset.UtcNow;
        var firstStart = new CastEvent(CastEventType.Started, "Bolt", 1);
        var complete = new CastEvent(CastEventType.Completed, "Bolt", 1);
        var secondStart = new CastEvent(CastEventType.Started, "Bolt", 2);
        var parser = new SequenceChatEventParser(
        [
            new ChatParseResult(null, [], firstStart),
            new ChatParseResult(null, [], complete),
            new ChatParseResult(null, [], secondStart)
        ]);
        var castCatalog = new FakeCastSpellCatalog(new CastSpellInfo("Bolt", 2.5, null));
        var overlay = new RecordingOverlayRenderer();

        var orchestrator = new GameLoopOrchestrator(
            new FakeChatCaptureService("ignored"),
            parser,
            castCatalog,
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now, CancellationToken.None);
        var firstStartedAt = overlay.LastSnapshot!.ActiveCast!.StartedAtUtc;

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(0.5), CancellationToken.None);
        Assert.Null(overlay.LastSnapshot!.ActiveCast);

        await orchestrator.TickAsync(new ScreenRegion(0, 0, 100, 30), ShardType.Eden, 10, now.AddSeconds(1.0), CancellationToken.None);
        Assert.NotNull(overlay.LastSnapshot!.ActiveCast);
        Assert.Equal("Bolt", overlay.LastSnapshot.ActiveCast!.SpellName);
        Assert.NotEqual(firstStartedAt, overlay.LastSnapshot.ActiveCast.StartedAtUtc);
    }

    private sealed class FakeChatCaptureService : IChatCaptureService
    {
        private readonly string _text;

        public FakeChatCaptureService(string text)
        {
            _text = text;
        }

        public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
        {
            return Task.FromResult(_text);
        }
    }

    private sealed class SequenceChatCaptureService : IChatCaptureService
    {
        private readonly Queue<string> _frames;

        public SequenceChatCaptureService(IEnumerable<string> frames)
        {
            _frames = new Queue<string>(frames);
        }

        public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
        {
            return Task.FromResult(_frames.Count > 0 ? _frames.Dequeue() : string.Empty);
        }
    }

    private sealed class FakeAdapterValueSource : IAdapterValueSource
    {
        public IReadOnlyDictionary<string, string> LatestAdapterValues { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RegionAwareCaptureService : IChatCaptureService, IOcrCaptureBatchDiagnostics
    {
        public List<ScreenRegion> Regions { get; } = [];
        public int BeginBatchCount { get; private set; }
        public int CompleteBatchCount { get; private set; }

        public void BeginCaptureBatch() => BeginBatchCount++;
        public void CompleteCaptureBatch() => CompleteBatchCount++;

        public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
        {
            Regions.Add(region);
            return Task.FromResult(region.X == 20
                ? "Str: 150 Con: 125 Dex: 188 Qui: 135 Int: 195 Cha: 190 Pie: 135 Emp: 135"
                : "You target [Training Dummy].");
        }
    }

    private sealed class CancelingCaptureService : IChatCaptureService, IOcrCaptureBatchDiagnostics
    {
        public int BeginBatchCount { get; private set; }
        public int CompleteBatchCount { get; private set; }

        public void BeginCaptureBatch() => BeginBatchCount++;
        public void CompleteCaptureBatch() => CompleteBatchCount++;

        public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken) =>
            Task.FromCanceled<string>(cancellationToken);
    }

    private sealed class FakeChatEventParser : IChatEventParser
    {
        private readonly ChatParseResult _result;

        public FakeChatEventParser(ChatParseResult result)
        {
            _result = result;
        }

        public ChatParseResult Parse(string ocrText, string? fallbackTargetName = null)
        {
            return _result;
        }
    }

    private sealed class FakeHeraldClientFactory : IHeraldClientFactory
    {
        private readonly IHeraldClient _client;

        public FakeHeraldClientFactory(IHeraldClient client)
        {
            _client = client;
        }

        public IHeraldClient Resolve(ShardType shardType)
        {
            return _client;
        }
    }

    private sealed class FakeHeraldClient : IHeraldClient
    {
        private readonly TargetProfile? _profile;

        public FakeHeraldClient(TargetProfile? profile)
        {
            _profile = profile;
        }

        public int CallCount { get; private set; }

        public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_profile);
        }
    }

    private sealed class ControlledHeraldClient : IHeraldClient
    {
        private readonly Dictionary<string, TaskCompletionSource<TargetProfile?>> _requests =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
        {
            var request = new TaskCompletionSource<TargetProfile?>();
            _requests[targetName] = request;
            return request.Task;
        }

        public void Complete(string targetName, TargetProfile? profile)
        {
            _requests[targetName].TrySetResult(profile);
        }
    }

    private sealed class MemoryTargetProfileCache : ITargetProfileCache
    {
        private readonly Dictionary<(ShardType Shard, string Name), TargetProfile> _profiles = [];

        public TargetProfile? Load(ShardType shardType, string targetName)
        {
            _profiles.TryGetValue((shardType, targetName.Trim().ToLowerInvariant()), out var profile);
            return profile;
        }

        public void Save(ShardType shardType, TargetProfile profile)
        {
            _profiles[(shardType, profile.Name.Trim().ToLowerInvariant())] = profile;
        }

        public void Delete(ShardType shardType, string targetName)
        {
            _profiles.Remove((shardType, targetName.Trim().ToLowerInvariant()));
        }
    }

    private sealed class NamedHeraldClient : IHeraldClient
    {
        private readonly IReadOnlyDictionary<string, TargetProfile?> _profiles;

        public NamedHeraldClient(IReadOnlyDictionary<string, TargetProfile?> profiles)
        {
            _profiles = profiles;
        }

        public int CallCount { get; private set; }

        public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
        {
            CallCount++;
            _profiles.TryGetValue(targetName, out var profile);
            return Task.FromResult(profile);
        }
    }

    private sealed class FakeCastSpellCatalog : ICastSpellCatalog
    {
        private readonly Dictionary<string, CastSpellInfo> _entries = new(StringComparer.OrdinalIgnoreCase);

        public FakeCastSpellCatalog(params CastSpellInfo[] entries)
        {
            foreach (var entry in entries)
            {
                _entries[entry.SpellName] = entry;
            }
        }

        public CastSpellInfo? FindBySpellName(string spellName)
        {
            _entries.TryGetValue(spellName, out var info);
            return info;
        }
    }

    private sealed class SequenceChatEventParser : IChatEventParser
    {
        private readonly Queue<ChatParseResult> _results;

        public SequenceChatEventParser(IEnumerable<ChatParseResult> results)
        {
            _results = new Queue<ChatParseResult>(results);
        }

        public ChatParseResult Parse(string ocrText, string? fallbackTargetName = null)
        {
            return _results.Count > 0 ? _results.Dequeue() : new ChatParseResult(null, []);
        }
    }

    private sealed class RecordingCcImmunityTracker : ICcImmunityTracker
    {
        public List<AbilityHit> RegisteredHits { get; } = [];

        public void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc)
        {
            RegisteredHits.Add(hit);
        }

        public IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc)
        {
            return [];
        }
    }

    private sealed class RecordingOverlayRenderer : IOverlayRenderer
    {
        public OverlaySnapshot? LastSnapshot { get; private set; }

        public Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken)
        {
            LastSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingResponseDiagnostics : IResponseDiagnostics
    {
        public List<string> Lines { get; } = [];

        public void Log(string message)
        {
            Lines.Add(message);
        }
    }
}
