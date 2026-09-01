using System.Text.Json;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;

namespace HeraldHelper.Desktop.Services;

public interface IWritableSettings<T> where T : class, new()
{
    T Value { get; }
    void Load();
    void Save();
    void Update(Action<T> applyChanges);
}

internal class SqliteWritableSettings<T> : IWritableSettings<T> where T : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    protected readonly SettingsController _settings;
    protected readonly string _key;

    public SqliteWritableSettings(SettingsController settings, string key)
    {
        _settings = settings;
        _key = key;
    }

    public T Value { get; private set; } = new();

    public virtual void Load()
    {
        var map = _settings.LoadMap();
        if (map.TryGetValue(_key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                if (loaded is not null)
                {
                    Value = loaded;
                    return;
                }
            }
            catch
            {
                // ignore and fall through to defaults
            }
        }

        Value = new();
    }

    public virtual void Save()
    {
        var raw = JsonSerializer.Serialize(Value, JsonOptions);
        _settings.Save([new ConfigEntry { Key = _key, Value = raw }]);
    }

    public virtual void Update(Action<T> applyChanges)
    {
        applyChanges(Value);
        Save();
    }
}

internal sealed class HeraldHelperSettingsService : SqliteWritableSettings<HeraldHelperSettings>
{
    private const string SettingsKey = "heraldhelper.settings.v1";

    public HeraldHelperSettingsService(SettingsController settings) : base(settings, SettingsKey)
    {
    }

    public override void Load()
    {
        base.Load();
        if (!_settings.LoadMap().ContainsKey(SettingsKey))
        {
            MigrateFromLegacy(_settings.LoadMap(), Value);
            Save();
        }
    }

    private static void MigrateFromLegacy(IReadOnlyDictionary<string, string> map, HeraldHelperSettings settings)
    {
        var overlay = settings.Overlay;
        overlay.X = ReadInt(map, "overlayX", overlay.X);
        overlay.Y = ReadInt(map, "overlayY", overlay.Y);
        overlay.TimerX = ReadInt(map, "overlayXTimer", overlay.TimerX);
        overlay.TimerY = ReadInt(map, "overlayYTimer", overlay.TimerY);
        overlay.CastX = ReadInt(map, "overlayXCast", overlay.CastX);
        overlay.CastY = ReadInt(map, "overlayYCast", overlay.CastY);
        overlay.FontSize = ReadInt(map, "fontSize", overlay.FontSize);
        overlay.TimerSize = ReadInt(map, "timerSize", overlay.TimerSize);
        overlay.TargetColor = ReadString(map, "targetColor", overlay.TargetColor);
        overlay.TimerColor = ReadString(map, "timerColor", overlay.TimerColor);
        overlay.OutlineColor = ReadString(map, "outlineColor", overlay.OutlineColor);
        overlay.ShowTarget = ReadBool(map, "overlayShowTarget", overlay.ShowTarget);
        overlay.ShowTimers = ReadBool(map, "overlayShowTimers", overlay.ShowTimers);
        overlay.ShowCastBar = ReadBool(map, "overlayShowCastBar", overlay.ShowCastBar);
        overlay.DynamicCastSpeedEnabled = ReadBool(map, "dynamicCastSpeedEnabled", overlay.DynamicCastSpeedEnabled);
        overlay.EstimatedSpellDamageEnabled = ReadBool(map, "estimatedSpellDamageEnabled", overlay.EstimatedSpellDamageEnabled);
        overlay.OcrReplayEnabled = ReadBool(map, "ocrReplayEnabled", overlay.OcrReplayEnabled);
        overlay.TargetFontFamily = ReadString(map, "targetFontFamily", overlay.TargetFontFamily);
        overlay.TimerFontFamily = ReadString(map, "timerFontFamily", overlay.TimerFontFamily);
        overlay.CastbarFontFamily = ReadString(map, "castbarFontFamily", overlay.CastbarFontFamily);

        settings.Appearance.Theme = ReadString(map, "ui.theme.mode", settings.Appearance.Theme);
        settings.Appearance.AccentColor = ReadString(map, "ui.theme.accent", settings.Appearance.AccentColor);
        settings.Auth.AutoRefreshEnabled = ReadBool(map, "auth.autoRefreshEnabled", settings.Auth.AutoRefreshEnabled);
        settings.Auth.AutoRefreshMinutes = ReadInt(map, "auth.autoRefreshMinutes", settings.Auth.AutoRefreshMinutes);
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
