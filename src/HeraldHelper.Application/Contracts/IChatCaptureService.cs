using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

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
