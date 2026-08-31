namespace HeraldHelper.Domain.Models;

public sealed record OcrWatchRegion(
    string Key,
    string Label,
    ScreenRegion Region);