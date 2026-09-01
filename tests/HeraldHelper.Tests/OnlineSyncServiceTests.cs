using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class OnlineSyncServiceTests
{
    [Fact]
    public void Mode_DefaultIsDisabled()
    {
        var settings = new FakeWritableSettings();
        var service = new OnlineSyncService(new NoOpOnlineTargetProfileClient(), new FakeRepository(), settings);

        Assert.Equal(OnlineSyncMode.Disabled, service.Mode);
    }

    [Fact]
    public async Task TryDownload_Disabled_ReturnsOffline()
    {
        var settings = new FakeWritableSettings();
        var service = new OnlineSyncService(new NoOpOnlineTargetProfileClient(), new FakeRepository(), settings);

        var result = await service.TryDownloadAsync(ShardType.Eden, "Teagan");

        Assert.True(result.Success);
        Assert.Equal("Online sync is disabled.", result.Error);
    }

    [Fact]
    public async Task TryUpload_Disabled_ReturnsOffline()
    {
        var settings = new FakeWritableSettings();
        var service = new OnlineSyncService(new NoOpOnlineTargetProfileClient(), new FakeRepository(), settings);

        var profile = new TargetProfile("Teagan", null, null, null, null, null);
        var result = await service.TryUploadAsync(ShardType.Eden, profile);

        Assert.True(result.Success);
        Assert.Equal("Online sync is disabled.", result.Error);
    }

    [Fact]
    public async Task TryDownload_ReadOnlyAndClientUnavailable_ReturnsFailed()
    {
        var settings = new FakeWritableSettings { Value = { Online = { Mode = OnlineSyncMode.ReadOnly } } };
        var service = new OnlineSyncService(new NoOpOnlineTargetProfileClient(), new FakeRepository(), settings);

        var result = await service.TryDownloadAsync(ShardType.Eden, "Teagan");

        Assert.False(result.Success);
        Assert.Equal("Online service is not available.", result.Error);
    }

    [Fact]
    public async Task TryDownload_ReadWrite_SavesToRepository()
    {
        var settings = new FakeWritableSettings { Value = { Online = { Mode = OnlineSyncMode.ReadWrite } } };
        var client = new StubOnlineClient { Downloaded = new TargetProfile("Teagan", "Guild", "Minstrel", 50, "RR5L0", 42) };
        var repository = new FakeRepository();
        var service = new OnlineSyncService(client, repository, settings);

        var result = await service.TryDownloadAsync(ShardType.Eden, "Teagan");

        Assert.True(result.Success);
        Assert.Equal("Teagan", result.Profile?.Name);
        Assert.Equal("Teagan", repository.SavedProfiles.Single().Name);
    }

    private sealed class FakeWritableSettings : IWritableSettings<HeraldHelperSettings>
    {
        public HeraldHelperSettings Value { get; } = new();

        public void Load() { }

        public void Save() { }

        public void Update(Action<HeraldHelperSettings> applyChanges)
        {
            applyChanges(Value);
        }
    }

    private sealed class FakeRepository : ITargetProfileRepository
    {
        public List<TargetProfile> SavedProfiles { get; } = [];

        public Task<TargetProfile?> GetAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
            => Task.FromResult<TargetProfile?>(null);

        public Task SaveAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
        {
            SavedProfiles.Add(profile);
            return Task.CompletedTask;
        }
    }

    private sealed class StubOnlineClient : IOnlineTargetProfileClient
    {
        public TargetProfile? Downloaded { get; set; }

        public Task<TargetProfile?> DownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Downloaded);

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
