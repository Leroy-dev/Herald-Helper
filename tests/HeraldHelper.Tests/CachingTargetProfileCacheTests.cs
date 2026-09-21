using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class CachingTargetProfileCacheTests
{
    [Fact]
    public void Load_ReturnsProfileFromBackendAndCachesIt()
    {
        var backend = new InMemoryTargetProfileCache();
        backend.Save(ShardType.Eden, new TargetProfile("Alice", "Guild", "Hero", 50, "RR5L0", 12));
        var cache = new CachingTargetProfileCache(backend, asyncWrites: false);

        var first = cache.Load(ShardType.Eden, "Alice");
        var second = cache.Load(ShardType.Eden, "Alice");

        Assert.NotNull(first);
        Assert.Equal("Alice", first!.Name);
        Assert.Equal(1, backend.LoadCallCount);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(1, cache.Misses);
    }

    [Fact]
    public void Load_RespectsMaxAge()
    {
        var backend = new InMemoryTargetProfileCache();
        var cache = new CachingTargetProfileCache(backend, maxAge: TimeSpan.FromMilliseconds(1), asyncWrites: false);

        backend.Save(ShardType.Eden, new TargetProfile("Alice", "Old Guild", "Hero", 50, "RR1L0", 0));

        var first = cache.Load(ShardType.Eden, "Alice");
        Assert.Equal("Old Guild", first!.Guild);

        Thread.Sleep(10);
        backend.Save(ShardType.Eden, new TargetProfile("Alice", "New Guild", "Hero", 50, "RR2L0", 0));

        var second = cache.Load(ShardType.Eden, "Alice");
        Assert.Equal("New Guild", second!.Guild);
        Assert.Equal(2, backend.LoadCallCount);
    }

    [Fact]
    public void Delete_EvictsMemoryAndBackend()
    {
        var backend = new InMemoryTargetProfileCache();
        backend.Save(ShardType.Eden, new TargetProfile("Alice", "Guild", "Hero", 50, "RR5L0", 12));
        var cache = new CachingTargetProfileCache(backend, asyncWrites: false);
        cache.Load(ShardType.Eden, "Alice"); // warm the memory layer

        cache.Delete(ShardType.Eden, "Alice");

        Assert.False(backend.Contains(ShardType.Eden, "Alice"));
        Assert.Null(cache.Load(ShardType.Eden, "Alice"));
    }

    [Fact]
    public void Save_WritesThroughAndUpdatesCache()
    {
        var backend = new InMemoryTargetProfileCache();
        var cache = new CachingTargetProfileCache(backend, asyncWrites: false);
        var profile = new TargetProfile("Alice", "Guild", "Hero", 50, "RR5L0", 12);

        cache.Save(ShardType.Eden, profile);
        var fromCache = cache.Load(ShardType.Eden, "Alice");

        Assert.NotNull(fromCache);
        Assert.Equal(0, backend.LoadCallCount);
        Assert.True(backend.Contains(ShardType.Eden, "Alice"));
    }

    [Fact]
    public async Task Save_AsyncWritesReachesBackend()
    {
        var backend = new InMemoryTargetProfileCache();
        var cache = new CachingTargetProfileCache(backend, asyncWrites: true);
        var profile = new TargetProfile("Alice", "Guild", "Hero", 50, "RR5L0", 12);

        cache.Save(ShardType.Eden, profile);
        cache.Dispose();

        var fromBackend = backend.Load(ShardType.Eden, "Alice");
        Assert.NotNull(fromBackend);
        await Task.Yield();
    }

    [Fact]
    public void Save_EnforcesMaxEntries()
    {
        var backend = new InMemoryTargetProfileCache();
        var cache = new CachingTargetProfileCache(backend, maxEntries: 2, asyncWrites: false);

        cache.Save(ShardType.Eden, new TargetProfile("Alice", null, "Hero", 50, null, null));
        cache.Save(ShardType.Eden, new TargetProfile("Bob", null, "Hero", 50, null, null));
        cache.Save(ShardType.Eden, new TargetProfile("Charlie", null, "Hero", 50, null, null));

        Assert.NotNull(cache.Load(ShardType.Eden, "Alice"));
        Assert.NotNull(cache.Load(ShardType.Eden, "Bob"));
        Assert.NotNull(cache.Load(ShardType.Eden, "Charlie"));
        Assert.True(cache.Evictions >= 1);
        Assert.True(backend.LoadCallCount >= 1);
    }

    [Fact]
    public void Diagnostics_LogsCacheHitAndMiss()
    {
        var backend = new InMemoryTargetProfileCache();
        backend.Save(ShardType.Eden, new TargetProfile("Alice", null, "Hero", 50, null, null));
        var diagnostics = new RecordingResponseDiagnostics();
        var cache = new CachingTargetProfileCache(backend, diagnostics, asyncWrites: false);

        cache.Load(ShardType.Eden, "Alice");
        cache.Load(ShardType.Eden, "Alice");

        Assert.Contains(diagnostics.Lines, l => l.Contains("miss"));
        Assert.Contains(diagnostics.Lines, l => l.Contains("hit"));
    }

    private sealed class InMemoryTargetProfileCache : ITargetProfileCache
    {
        private readonly Dictionary<(ShardType, string), TargetProfile> _profiles = [];

        public int LoadCallCount { get; private set; }

        public TargetProfile? Load(ShardType shardType, string targetName)
        {
            LoadCallCount++;
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

        public bool Contains(ShardType shardType, string targetName)
        {
            return _profiles.ContainsKey((shardType, targetName.Trim().ToLowerInvariant()));
        }
    }

    private sealed class RecordingResponseDiagnostics : IResponseDiagnostics
    {
        public List<string> Lines { get; } = [];

        public void Log(string message) => Lines.Add(message);
    }
}
