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

    public void UpdateOverlay(Action<OverlaySettings> mutate)
    {
        _settings.Update(s => mutate(s.Overlay));
    }

    public OverlaySettings WithDefaults(OverlaySettings current, int fontSize, int timerSize)
    {
        current.FontSize = fontSize > 0 ? fontSize : current.FontSize;
        current.TimerSize = timerSize > 0 ? timerSize : current.TimerSize;
        return current;
    }
}
