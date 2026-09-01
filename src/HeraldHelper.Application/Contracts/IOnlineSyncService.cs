using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface IOnlineSyncService
{
    OnlineSyncMode Mode { get; }

    Task<SyncResult> TryDownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default);

    Task<SyncResult> TryUploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default);
}
