using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface ITargetProfileRepository
{
    Task<TargetProfile?> GetAsync(ShardType shard, string name, CancellationToken cancellationToken = default);

    Task SaveAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default);
}
