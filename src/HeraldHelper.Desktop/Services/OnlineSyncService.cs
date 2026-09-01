using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Services;

internal sealed class OnlineSyncService : IOnlineSyncService
{
    private readonly IOnlineTargetProfileClient _client;
    private readonly ITargetProfileRepository _repository;
    private readonly IWritableSettings<HeraldHelperSettings> _settings;

    public OnlineSyncService(
        IOnlineTargetProfileClient client,
        ITargetProfileRepository repository,
        IWritableSettings<HeraldHelperSettings> settings)
    {
        _client = client;
        _repository = repository;
        _settings = settings;
    }

    public OnlineSyncMode Mode => _settings.Value.Online.Mode;

    public async Task<SyncResult> TryDownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
    {
        if (Mode == OnlineSyncMode.Disabled)
        {
            return SyncResult.Offline;
        }

        if (!await _client.IsAvailableAsync(cancellationToken))
        {
            return SyncResult.Failed("Online service is not available.");
        }

        var profile = await _client.DownloadAsync(shard, name, cancellationToken);
        if (profile is null)
        {
            return SyncResult.Failed("No profile found online.");
        }

        if (Mode == OnlineSyncMode.ReadWrite)
        {
            await _repository.SaveAsync(shard, profile, cancellationToken);
        }

        return SyncResult.Ok(profile);
    }

    public async Task<SyncResult> TryUploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
    {
        if (Mode != OnlineSyncMode.ReadWrite)
        {
            return SyncResult.Offline;
        }

        if (!await _client.IsAvailableAsync(cancellationToken))
        {
            return SyncResult.Failed("Online service is not available.");
        }

        var uploaded = await _client.UploadAsync(shard, profile, cancellationToken);
        return uploaded ? SyncResult.Ok(profile) : SyncResult.Failed("Upload failed.");
    }
}
