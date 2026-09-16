using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Tests;

public sealed class RuntimeLoopTests
{
    [Fact]
    public async Task TickOnceAsync_WithChatRegion_FiresTickCompleted()
    {
        var session = new FakeRuntimeSession();
        using var loop = new RuntimeLoop(
            session,
            () => new LoopTickInput(new ScreenRegion(0, 0, 10, 10), ShardType.Eden, 30),
            TimeSpan.FromMinutes(1));
        LoopTickResult? completed = null;
        loop.TickCompleted += tick => completed = tick;

        await loop.TickOnceAsync();

        Assert.Equal(1, session.TickCount);
        Assert.Equal("out", completed?.Output);
        Assert.Equal("diag", completed?.DiagnosticsText);
    }

    [Fact]
    public async Task TickOnceAsync_WithoutRegionOrWatchRegions_FiresTickFailed()
    {
        var session = new FakeRuntimeSession();
        using var loop = new RuntimeLoop(
            session,
            () => new LoopTickInput(null, ShardType.Default, 0),
            TimeSpan.FromMinutes(1));
        string? failure = null;
        loop.TickFailed += message => failure = message;

        await loop.TickOnceAsync();

        Assert.Equal(0, session.TickCount);
        Assert.Equal("Select chat area first (drag selection).", failure);
    }

    [Fact]
    public async Task TickOnceAsync_WhileTickInFlight_DoesNotReenter()
    {
        var session = new FakeRuntimeSession { BlockTick = true };
        using var loop = new RuntimeLoop(
            session,
            () => new LoopTickInput(new ScreenRegion(0, 0, 10, 10), ShardType.Eden, 0),
            TimeSpan.FromMinutes(1));

        var first = loop.TickOnceAsync();
        await session.TickEntered; // tick work now runs on a pool thread
        var second = loop.TickOnceAsync();

        Assert.Equal(1, session.TickCount);
        session.ReleaseTick();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public void StartStop_TogglesIsRunning()
    {
        var session = new FakeRuntimeSession();
        using var loop = new RuntimeLoop(
            session,
            () => new LoopTickInput(null, ShardType.Default, 0),
            TimeSpan.FromMinutes(1));

        Assert.False(loop.IsRunning);
        loop.Start();
        Assert.True(loop.IsRunning);
        loop.Stop();
        Assert.False(loop.IsRunning);
    }

    private sealed class FakeRuntimeSession : IRuntimeSession
    {
        private readonly TaskCompletionSource<LoopTickResult> _blocked = new();
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool BlockTick { get; set; }

        public Task TickEntered => _entered.Task;

        private int _tickCount;
        public int TickCount => _tickCount;

        public AppRuntimeSettings RuntimeSettings { get; } = new(
            null,
            ShardType.Eden,
            0,
            OcrEngineMode.Adaptive,
            []);

        public OverlaySnapshot? LastSnapshot => null;

        public Task<LoopTickResult> TickAsync(
            ScreenRegion? chatRegion,
            ShardType shard,
            int resistPercent,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _tickCount);
            _entered.TrySetResult();
            return BlockTick
                ? _blocked.Task
                : Task.FromResult(new LoopTickResult("out", null, "diag"));
        }

        public void ReleaseTick()
        {
            _blocked.TrySetResult(new LoopTickResult("out", null, "diag"));
        }

        public void Dispose()
        {
        }
    }
}
