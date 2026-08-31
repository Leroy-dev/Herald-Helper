using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

public sealed record DaocWindowDefinition(
    string Key,
    string Label,
    ScreenRegion Region,
    bool SizeIsEstimated = false,
    string? SizeSource = null)
{
    public string DisplayText => $"{Label} ({Region.X},{Region.Y} {(SizeIsEstimated ? "~" : string.Empty)}{Region.Width}x{Region.Height})";
}
