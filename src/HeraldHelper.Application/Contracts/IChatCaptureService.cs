using HeraldHelper.Domain.Models;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Contracts;

public interface IChatCaptureService
{
    Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken);
}

public interface IWindowAwareChatCaptureService
{
    Task<string> CaptureWindowTextAsync(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken);
}
