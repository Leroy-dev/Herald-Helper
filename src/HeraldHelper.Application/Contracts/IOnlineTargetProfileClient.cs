using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface IOnlineTargetProfileClient
{
    Task<TargetProfile?> DownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default);

    Task<bool> UploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
