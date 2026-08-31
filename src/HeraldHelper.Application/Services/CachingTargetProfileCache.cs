using System.Collections.Concurrent;
using System.Diagnostics;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public sealed class CachingTargetProfileCache : ITargetProfileCache, IDisposable
{
    private readonly ITargetProfileCache _backend;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly ConcurrentDictionary<(ShardType Shard, string NormalizedName), CacheEntry> _memory = new();
    private readonly ConcurrentDictionary<Guid, Task> _pendingWrites = new();
    private long _hits;
    private long _misses;
    private long _evictions;

    public CachingTargetProfileCache(
        ITargetProfileCache backend,
        IResponseDiagnostics? diagnostics = null,
        TimeSpan? maxAge = null,
        int maxEntries = 1000,
        bool asyncWrites = true)
    {
        _backend = backend;
        _diagnostics = diagnostics;
        MaxAge = maxAge ?? TimeSpan.FromMinutes(10);
        MaxEntries = Math.Max(1, maxEntries);
        AsyncWrites = asyncWrites;
    }

    public TimeSpan MaxAge { get; }
    public int MaxEntries { get; }
    public bool AsyncWrites { get; }

    public ITargetProfileCache Backend => _backend;

    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);
    public long Evictions => Interlocked.Read(ref _evictions);

    public TargetProfile? Load(ShardType shardType, string targetName)
    {
        var key = NormalizeKey(shardType, targetName);
        var now = DateTimeOffset.UtcNow;

        if (_memory.TryGetValue(key, out var entry))
        {
            if (now - entry.Timestamp < MaxAge)
            {
                Interlocked.Increment(ref _hits);
                _diagnostics?.Log($"[Cache] hit | {key.Shard}/{key.NormalizedName}");
                _memory[key] = entry with { LastAccess = now };
                return entry.Profile;
            }

            _memory.TryRemove(key, out _);
        }

        Interlocked.Increment(ref _misses);
        _diagnostics?.Log($"[Cache] miss | {key.Shard}/{key.NormalizedName}");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var profile = _backend.Load(shardType, targetName);
            stopwatch.Stop();
            _diagnostics?.Log($"[Cache] backend load | {key.Shard}/{key.NormalizedName} | {stopwatch.ElapsedMilliseconds} ms");

            if (profile is not null)
            {
                AddOrUpdate(key, profile, now);
            }

            return profile;
        }
        catch (Exception ex)
        {
            _diagnostics?.Log($"[Cache] backend load failed | {key.Shard}/{key.NormalizedName} | {ex.Message}");
            throw;
        }
    }

    public void Save(ShardType shardType, TargetProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return;
        }

        var key = NormalizeKey(shardType, profile.Name);
        var now = DateTimeOffset.UtcNow;

        AddOrUpdate(key, profile, now);

        if (AsyncWrites)
        {
            var id = Guid.NewGuid();
            var task = Task.Run(() =>
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    _backend.Save(shardType, profile);
                    stopwatch.Stop();
                    _diagnostics?.Log($"[Cache] write completed | {key.Shard}/{key.NormalizedName} | {stopwatch.ElapsedMilliseconds} ms");
                }
                catch (Exception ex)
                {
                    _diagnostics?.Log($"[Cache] write failed | {key.Shard}/{key.NormalizedName} | {ex.Message}");
                }
                finally
                {
                    _pendingWrites.TryRemove(id, out _);
                }
            });

            _pendingWrites[id] = task;
            _diagnostics?.Log($"[Cache] write queued | {key.Shard}/{key.NormalizedName}");
        }
        else
        {
            _backend.Save(shardType, profile);
            _diagnostics?.Log($"[Cache] write sync | {key.Shard}/{key.NormalizedName}");
        }
    }

    public void Dispose()
    {
        if (_pendingWrites.IsEmpty)
        {
            return;
        }

        try
        {
            Task.WaitAll(_pendingWrites.Values.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
    }

    private void AddOrUpdate((ShardType Shard, string NormalizedName) key, TargetProfile profile, DateTimeOffset now)
    {
        _memory.AddOrUpdate(
            key,
            _ => new CacheEntry(profile, now, now),
            (_, existing) => new CacheEntry(profile, now, now));

        EnforceRetention();
    }

    private void EnforceRetention()
    {
        while (_memory.Count > MaxEntries)
        {
            var oldest = _memory.MinBy(x => x.Value.LastAccess).Key;
            if (_memory.TryRemove(oldest, out _))
            {
                Interlocked.Increment(ref _evictions);
                _diagnostics?.Log($"[Cache] evicted | {oldest.Shard}/{oldest.NormalizedName}");
            }
        }
    }

    private static (ShardType Shard, string NormalizedName) NormalizeKey(ShardType shard, string name)
    {
        return (shard, name.Trim().ToLowerInvariant());
    }

    private sealed record CacheEntry(TargetProfile Profile, DateTimeOffset Timestamp, DateTimeOffset LastAccess);
}
