using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Services;

internal sealed class NoOpOnlineTargetProfileClient : IOnlineTargetProfileClient
{
    public Task<TargetProfile?> DownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
        => Task.FromResult<TargetProfile?>(null);

    public Task<bool> UploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
