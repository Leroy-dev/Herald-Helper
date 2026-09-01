using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class OverlaySettingsController
{
    private readonly IWritableSettings<HeraldHelperSettings> _settings;

    public OverlaySettingsController(IWritableSettings<HeraldHelperSettings> settings)
    {
        _settings = settings;
    }

    public OverlaySettings Load()
    {
        return _settings.Value.Overlay;
    }

    public void Save(OverlaySettings overlay)
    {
        _settings.Update(s => s.Overlay = overlay);
    }

    public void SaveVisibility(
        bool showTarget,
        bool showTimers,
        bool showCastBar,
        bool useRealmColors,
        bool dynamicCastSpeed,
        bool estimatedSpellDamage,
        bool ocrReplay)
    {
        _settings.Update(s =>
        {
            s.Overlay.ShowTarget = showTarget;
            s.Overlay.ShowTimers = showTimers;
            s.Overlay.ShowCastBar = showCastBar;
            s.Overlay.UseRealmColors = useRealmColors;
            s.Overlay.DynamicCastSpeedEnabled = dynamicCastSpeed;
            s.Overlay.EstimatedSpellDamageEnabled = estimatedSpellDamage;
            s.Overlay.OcrReplayEnabled = ocrReplay;
        });
    }

    public OverlaySettings WithDefaults(OverlaySettings current, int fontSize, int timerSize)
    {
        current.FontSize = fontSize > 0 ? fontSize : current.FontSize;
        current.TimerSize = timerSize > 0 ? timerSize : current.TimerSize;
        return current;
    }
}
