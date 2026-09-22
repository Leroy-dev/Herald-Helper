using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public interface IShardAuthRefreshService
{
    /// <summary>Silent headless harvest of the persistent profile's cookies.
    /// Returns null when the shard has no profile or the stored session is no
    /// longer valid — in that case the user must log in via OpenBrowserAsync.</summary>
    Task<ShardAuthBundle?> RefreshAsync(ShardType shard, CancellationToken cancellationToken);

    /// <summary>Opens a headed Chromium window for interactive login. The
    /// window stays open until the USER closes it (or a 10-minute cap);
    /// the last cookie snapshot is then validated — required cookies plus the
    /// hub page no longer showing login affordances — and returned (also
    /// reported via the OnRefreshed callback). Null when nothing valid was
    /// captured.</summary>
    Task<ShardAuthBundle?> OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken);
}
