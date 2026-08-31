namespace HeraldHelper.Desktop.Models;

public sealed record OverlaySettings(
    int X,
    int Y,
    int TimerX,
    int TimerY,
    int CastX,
    int CastY,
    int FontSize,
    int TimerSize,
    string TargetColor,
    string TimerColor,
    string OutlineColor,
    bool ShowTarget,
    bool ShowTimers,
    bool ShowCastBar,
    bool DynamicCastSpeedEnabled,
    bool EstimatedSpellDamageEnabled,
    bool OcrReplayEnabled);
