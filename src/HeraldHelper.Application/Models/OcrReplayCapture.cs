using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Models;

public sealed record OcrReplayCapture(
    string Label,
    ScreenRegion Region,
    byte[] PngBytes,
    string OcrText,
    string EngineName);
