using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public interface IShardAuthRefreshService
{
    /// <summary>Silent headless harvest of the persistent profile's cookies.
    /// Returns null when the shard has no profile or the stored session is no
    /// longer valid — in that case the user must log in via OpenBrowserAsync.</summary>
    Task<ShardAuthBundle?> RefreshAsync(ShardType shard, CancellationToken cancellationToken);

    /// <summary>Opens a headed Chromium window for interactive login. Polls
    /// until a valid cookie set appears, then closes the window and returns
    /// the harvested bundle (also reported via the OnRefreshed callback).</summary>
    Task<ShardAuthBundle?> OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken);
}
