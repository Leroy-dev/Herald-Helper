using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Services;

internal sealed class LocalTargetProfileRepository : ITargetProfileRepository
{
    private readonly ITargetProfileCache _cache;

    public LocalTargetProfileRepository(ITargetProfileCache cache)
    {
        _cache = cache;
    }

    public Task<TargetProfile?> GetAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.Load(shard, name));
    }

    public Task SaveAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
    {
        _cache.Save(shard, profile);
        return Task.CompletedTask;
    }
}
