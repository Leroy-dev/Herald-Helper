using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Models;

/// <summary>
/// What the overlay windows were last told to draw — projected so the Live
/// view can mirror the same state inside the app.
/// </summary>
public sealed record OverlayViewState(
    string TargetText,
    string TimerText,
    IReadOnlyList<(string Text, System.Windows.Media.Color Color, IconSpriteRef? Icon)>? TimerLines,
    IReadOnlyList<(string Text, System.Windows.Media.Color Color, IconSpriteRef? Icon)>? ResistsLines,
    CastBarState? Cast,
    System.Windows.Media.Color TargetColor,
    System.Windows.Media.Color TimerColor,
    System.Windows.Media.Color OutlineColor,
    string TargetFontFamily,
    string TimerFontFamily,
    int TargetFontSize,
    int TimerFontSize,
    int ResistsFontSize,
    DateTimeOffset RenderedAtUtc);
