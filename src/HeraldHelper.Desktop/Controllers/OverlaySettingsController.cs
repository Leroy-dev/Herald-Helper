using System.Globalization;
using HeraldHelper.Desktop.Models;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class OverlaySettingsController
{
    private readonly SettingsController _settings;

    public OverlaySettingsController(SettingsController settings)
    {
        _settings = settings;
    }

    public OverlaySettings Load()
    {
        var map = _settings.LoadMap();
        return new OverlaySettings(
            ReadInt(map, "overlayX", 1200),
            ReadInt(map, "overlayY", 900),
            ReadInt(map, "overlayXTimer", 1580),
            ReadInt(map, "overlayYTimer", 900),
            ReadInt(map, "overlayXCast", 1200),
            ReadInt(map, "overlayYCast", 986),
            ReadInt(map, "fontSize", 20),
            ReadInt(map, "timerSize", 20),
            ReadString(map, "targetColor", "#FFFFFF"),
            ReadString(map, "timerColor", "#FFFFFF"),
            ReadString(map, "outlineColor", "#000000"),
            ReadBool(map, "overlayShowTarget", true),
            ReadBool(map, "overlayShowTimers", true),
            ReadBool(map, "overlayShowCastBar", true),
            ReadBool(map, "dynamicCastSpeedEnabled", false),
            ReadBool(map, "estimatedSpellDamageEnabled", false),
            ReadBool(map, "ocrReplayEnabled", false));
    }

    public IEnumerable<ConfigEntry> Save(OverlaySettings settings)
    {
        return
        [
            new ConfigEntry { Key = "overlayX", Value = settings.X.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "overlayY", Value = settings.Y.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "overlayXTimer", Value = settings.TimerX.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "overlayYTimer", Value = settings.TimerY.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "overlayXCast", Value = settings.CastX.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "overlayYCast", Value = settings.CastY.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "fontSize", Value = settings.FontSize.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "timerSize", Value = settings.TimerSize.ToString(CultureInfo.InvariantCulture) },
            new ConfigEntry { Key = "targetColor", Value = settings.TargetColor },
            new ConfigEntry { Key = "timerColor", Value = settings.TimerColor },
            new ConfigEntry { Key = "outlineColor", Value = settings.OutlineColor },
            new ConfigEntry { Key = "overlayShowTarget", Value = settings.ShowTarget ? "1" : "0" },
            new ConfigEntry { Key = "overlayShowTimers", Value = settings.ShowTimers ? "1" : "0" },
            new ConfigEntry { Key = "overlayShowCastBar", Value = settings.ShowCastBar ? "1" : "0" },
            new ConfigEntry { Key = "dynamicCastSpeedEnabled", Value = settings.DynamicCastSpeedEnabled ? "1" : "0" },
            new ConfigEntry { Key = "estimatedSpellDamageEnabled", Value = settings.EstimatedSpellDamageEnabled ? "1" : "0" },
            new ConfigEntry { Key = "ocrReplayEnabled", Value = settings.OcrReplayEnabled ? "1" : "0" }
        ];
    }

    public IEnumerable<ConfigEntry> SaveVisibility(
        bool showTarget,
        bool showTimers,
        bool showCastBar,
        bool dynamicCastSpeed,
        bool estimatedSpellDamage,
        bool ocrReplay)
    {
        return
        [
            new ConfigEntry { Key = "overlayShowTarget", Value = showTarget ? "1" : "0" },
            new ConfigEntry { Key = "overlayShowTimers", Value = showTimers ? "1" : "0" },
            new ConfigEntry { Key = "overlayShowCastBar", Value = showCastBar ? "1" : "0" },
            new ConfigEntry { Key = "dynamicCastSpeedEnabled", Value = dynamicCastSpeed ? "1" : "0" },
            new ConfigEntry { Key = "estimatedSpellDamageEnabled", Value = estimatedSpellDamage ? "1" : "0" },
            new ConfigEntry { Key = "ocrReplayEnabled", Value = ocrReplay ? "1" : "0" }
        ];
    }

    public OverlaySettings WithDefaults(OverlaySettings current, int fontSize, int timerSize)
    {
        return current with
        {
            FontSize = fontSize > 0 ? fontSize : current.FontSize,
            TimerSize = timerSize > 0 ? timerSize : current.TimerSize
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
    {
        return int.TryParse(map.TryGetValue(key, out var raw) ? raw : null, out var parsed) ? parsed : fallback;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        return map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => fallback
        };
    }
}
