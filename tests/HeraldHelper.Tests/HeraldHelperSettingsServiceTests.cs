using System.Text.Json;
using HeraldHelper.Desktop;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;

namespace HeraldHelper.Tests;

public sealed class HeraldHelperSettingsServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDataStore _store;

    public HeraldHelperSettingsServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hh_settings_test_{Guid.NewGuid()}.db");
        _store = new AppDataStore(_dbPath);
        _store.Initialize();
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public void Load_FirstTime_ReturnsDefaults()
    {
        var service = CreateService();

        service.Load();

        Assert.NotNull(service.Value);
        Assert.Equal(1200, service.Value.Overlay.X);
        Assert.Equal(20, service.Value.Overlay.FontSize);
        Assert.Equal("#FFFFFF", service.Value.Overlay.TargetColor);
        Assert.True(service.Value.Overlay.ShowTarget);
        Assert.Equal("Segoe UI", service.Value.Overlay.TargetFontFamily);
        Assert.Equal("Dark", service.Value.Appearance.Theme);
        Assert.Equal("#007ACC", service.Value.Appearance.AccentColor);
        Assert.True(service.Value.Auth.AutoRefreshEnabled);
        Assert.Equal(25, service.Value.Auth.AutoRefreshMinutes);
    }

    [Fact]
    public void Load_LegacyFlatKeys_MigratesToTypedSettings()
    {
        var settingsController = new SettingsController(_store);
        settingsController.Save([
            new ConfigEntry { Key = "overlayX", Value = "1500" },
            new ConfigEntry { Key = "overlayY", Value = "950" },
            new ConfigEntry { Key = "fontSize", Value = "24" },
            new ConfigEntry { Key = "timerSize", Value = "18" },
            new ConfigEntry { Key = "targetColor", Value = "#00FF00" },
            new ConfigEntry { Key = "targetFontFamily", Value = "Arial" },
            new ConfigEntry { Key = "ocrReplayEnabled", Value = "1" },
            new ConfigEntry { Key = "some.other.setting", Value = "keep" }
        ]);

        var service = CreateService();
        service.Load();

        Assert.Equal(1500, service.Value.Overlay.X);
        Assert.Equal(950, service.Value.Overlay.Y);
        Assert.Equal(24, service.Value.Overlay.FontSize);
        Assert.Equal(18, service.Value.Overlay.TimerSize);
        Assert.Equal("#00FF00", service.Value.Overlay.TargetColor);
        Assert.Equal("Arial", service.Value.Overlay.TargetFontFamily);
        Assert.True(service.Value.Overlay.OcrReplayEnabled);

        var map = _store.LoadSettingsMap();
        Assert.True(map.ContainsKey("some.other.setting"));
        Assert.Equal("keep", map["some.other.setting"]);
        Assert.True(map.ContainsKey("heraldhelper.settings.v1"));
    }

    [Fact]
    public void Load_LegacyFlatKeys_MigratesThemeAndAuth()
    {
        var settingsController = new SettingsController(_store);
        settingsController.Save([
            new ConfigEntry { Key = "ui.theme.mode", Value = "light" },
            new ConfigEntry { Key = "ui.theme.accent", Value = "#FF5733" },
            new ConfigEntry { Key = "auth.autoRefreshEnabled", Value = "false" },
            new ConfigEntry { Key = "auth.autoRefreshMinutes", Value = "60" }
        ]);

        var service = CreateService();
        service.Load();

        Assert.Equal("light", service.Value.Appearance.Theme);
        Assert.Equal("#FF5733", service.Value.Appearance.AccentColor);
        Assert.False(service.Value.Auth.AutoRefreshEnabled);
        Assert.Equal(60, service.Value.Auth.AutoRefreshMinutes);
    }

    [Fact]
    public void Save_PreservesUnrelatedSettings()
    {
        var settingsController = new SettingsController(_store);
        settingsController.Save([
            new ConfigEntry { Key = "unrelated", Value = "value" }
        ]);

        var service = CreateService();
        service.Load();
        var overlay = service.Value.Overlay;
        overlay.FontSize = 42;
        overlay.TargetFontFamily = "Consolas";
        Assert.Equal(42, overlay.FontSize);
        Assert.Same(overlay, service.Value.Overlay);
        Assert.Equal(42, service.Value.Overlay.FontSize);
        Assert.Equal("Consolas", service.Value.Overlay.TargetFontFamily);
        service.Save();

        var map = _store.LoadSettingsMap();
        Assert.True(map.ContainsKey("unrelated"));
        Assert.Equal("value", map["unrelated"]);

        var loaded = JsonSerializer.Deserialize<HeraldHelperSettings>(
            map["heraldhelper.settings.v1"],
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.Overlay.FontSize);
        Assert.Equal("Consolas", loaded.Overlay.TargetFontFamily);
    }

    [Fact]
    public void Save_BlobIsPreferredOverLegacyKeys()
    {
        var settingsController = new SettingsController(_store);
        var blob = new HeraldHelperSettings
        {
            Overlay = { X = 999, TargetFontFamily = "Verdana" }
        };
        settingsController.Save([
            new ConfigEntry { Key = "heraldhelper.settings.v1", Value = JsonSerializer.Serialize(blob) },
            new ConfigEntry { Key = "overlayX", Value = "1500" }
        ]);

        var service = CreateService();
        service.Load();

        Assert.Equal(999, service.Value.Overlay.X);
        Assert.Equal("Verdana", service.Value.Overlay.TargetFontFamily);
    }

    private HeraldHelperSettingsService CreateService()
    {
        var settingsController = new SettingsController(_store);
        return new HeraldHelperSettingsService(settingsController);
    }
}
