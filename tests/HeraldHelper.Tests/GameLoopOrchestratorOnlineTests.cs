using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class GameLoopOrchestratorOnlineTests
{
    [Fact]
    public async Task TickAsync_UsesOnlineProfileWhenAvailable()
    {
        var expected = new TargetProfile("TargetA", "OnlineGuild", "Hero", 50, "RR5L0", 12);
        var onlineSync = new FakeOnlineSyncService { Mode = OnlineSyncMode.ReadWrite };
        onlineSync.Profiles[(ShardType.Eden, "TargetA")] = expected;

        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            new RecordingCcImmunityTracker(),
            overlay,
            onlineSync: onlineSync);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 0,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("OnlineGuild", overlay.LastSnapshot.Target?.Guild);
    }

    [Fact]
    public async Task TickAsync_FallsBackToHeraldWhenOnlineReturnsNull()
    {
        var expected = new TargetProfile("TargetA", "HeraldGuild", "Hero", 50, "RR5L0", 12);
        var onlineSync = new FakeOnlineSyncService { Mode = OnlineSyncMode.ReadWrite };
        var capture = new FakeChatCaptureService("ignored");
        var parser = new FakeChatEventParser(new ChatParseResult(new TargetEvent("TargetA", TargetMembership.Member), []));
        var overlay = new RecordingOverlayRenderer();
        var orchestrator = new GameLoopOrchestrator(
            capture,
            parser,
            new FakeCastSpellCatalog(),
            new FakeHeraldClientFactory(new FakeHeraldClient(expected)),
            new RecordingCcImmunityTracker(),
            overlay,
            onlineSync: onlineSync);

        await orchestrator.TickAsync(
            new ScreenRegion(0, 0, 100, 30),
            ShardType.Eden,
            resistPercent: 0,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.NotNull(overlay.LastSnapshot);
        Assert.Equal("TargetA", overlay.LastSnapshot!.Target?.Name);
        Assert.Equal("HeraldGuild", overlay.LastSnapshot.Target?.Guild);
    }

    private sealed class FakeOnlineSyncService : IOnlineSyncService
    {
        public OnlineSyncMode Mode { get; set; } = OnlineSyncMode.Disabled;

        public Dictionary<(ShardType Shard, string Name), TargetProfile> Profiles { get; } = new();

        public Task<SyncResult> TryDownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
        {
            if (Profiles.TryGetValue((shard, name), out var profile))
            {
                return Task.FromResult(SyncResult.Ok(profile));
            }

            return Task.FromResult(SyncResult.Failed("Not found"));
        }

        public Task<SyncResult> TryUploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
            => Task.FromResult(SyncResult.Ok(profile));
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

    private sealed class FakeChatEventParser : IChatEventParser
    {
        private readonly ChatParseResult _result;

        public FakeChatEventParser(ChatParseResult result)
        {
            _result = result;
        }

        public ChatParseResult Parse(string text, string? fallbackTargetName = null) => _result;
    }

    private sealed class FakeCastSpellCatalog : ICastSpellCatalog
    {
        public CastSpellInfo? FindBySpellName(string spellName)
        {
            return null;
        }
    }

    private sealed class FakeHeraldClientFactory : IHeraldClientFactory
    {
        private readonly IHeraldClient _client;

        public FakeHeraldClientFactory(IHeraldClient client)
        {
            _client = client;
        }

        public IHeraldClient Resolve(ShardType shard) => _client;
    }

    private sealed class FakeHeraldClient : IHeraldClient
    {
        private readonly TargetProfile? _profile;

        public FakeHeraldClient(TargetProfile? profile)
        {
            _profile = profile;
        }

        public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
            => Task.FromResult(_profile);
    }

    private sealed class RecordingCcImmunityTracker : ICcImmunityTracker
    {
        public void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc) { }

        public void RetractFreshEntries(IEnumerable<string> targetNames, DateTimeOffset nowUtc, TimeSpan maxAge) { }

        public IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc) => [];
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
}
