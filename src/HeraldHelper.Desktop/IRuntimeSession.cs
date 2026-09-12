using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Desktop;

internal interface IRuntimeSession : IDisposable
{
    AppRuntimeSettings RuntimeSettings { get; }

    OverlaySnapshot? LastSnapshot { get; }

    Task<LoopTickResult> TickAsync(
        ScreenRegion? chatRegion,
        ShardType shard,
        int resistPercent,
        CancellationToken cancellationToken);
}
