using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public interface IShardAuthRefreshService
{
    Task<ShardAuthBundle?> RefreshAsync(ShardType shard, CancellationToken cancellationToken);
    Task OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken);
}
