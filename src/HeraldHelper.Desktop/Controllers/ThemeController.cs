using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using HeraldHelper.Desktop.Models;
using MaterialDesignThemes.Wpf;
using Color = System.Windows.Media.Color;

namespace HeraldHelper.Desktop.Controllers;

public sealed class ThemeController
{
    private readonly PaletteHelper _paletteHelper = new();
    private readonly SettingsController _settingsController;

    public ThemeController(SettingsController settingsController)
    {
        _settingsController = settingsController;
    }

    public ApplicationTheme Mode { get; private set; } = ApplicationTheme.Dark;
    public Color Accent { get; private set; } = Color.FromRgb(0x00, 0x7A, 0xCC);

    public void Initialize()
    {
        var settings = _settingsController.LoadMap();
        var mode = settings.TryGetValue("ui.theme.mode", out var modeRaw) ? modeRaw : "dark";
        Mode = mode.ToLowerInvariant() switch
        {
            "light" => ApplicationTheme.Light,
            "system" => ApplicationTheme.System,
            _ => ApplicationTheme.Dark
        };

        var accentRaw = settings.TryGetValue("ui.theme.accent", out var accentValue) ? accentValue : "#007ACC";
        Accent = TryParseColor(accentRaw) ?? Color.FromRgb(0x00, 0x7A, 0xCC);

        Apply(Mode, Accent);
    }

    public void Apply(ApplicationTheme mode, Color accent)
    {
        Mode = mode;
        Accent = accent;

        var dark = mode switch
        {
            ApplicationTheme.Light => false,
            ApplicationTheme.Dark => true,
            _ => IsSystemDark()
        };

        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);

        ApplyVsCodePalette(dark, accent);
    }

    public void Save(ApplicationTheme mode, Color accent)
    {
        Mode = mode;
        Accent = accent;
        _settingsController.Save([
            new ConfigEntry { Key = "ui.theme.mode", Value = mode.ToString().ToLowerInvariant() },
            new ConfigEntry { Key = "ui.theme.accent", Value = ToHex(accent) }
        ]);
        Apply(mode, accent);
    }

    public void Reset()
    {
        Save(ApplicationTheme.Dark, Color.FromRgb(0x00, 0x7A, 0xCC));
    }

    public bool IsDark => Mode switch
    {
        ApplicationTheme.Light => false,
        ApplicationTheme.Dark => true,
        _ => IsSystemDark()
    };

    public string ToggleButtonContent => IsDark ? "Switch to Light" : "Switch to Dark";

    public void ToggleDarkLight()
    {
        var mode = IsDark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        Save(mode, Accent);
    }

    private static bool IsSystemDark()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int light)
                {
                    return light == 0;
                }
            }
            catch
            {
            }
        }

        return true;
    }

    private static void ApplyVsCodePalette(bool dark, Color accent)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        var resources = System.Windows.Application.Current.Resources;
        if (dark)
        {
            resources["VsWindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["VsTitleBarBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelAltBrush"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
            resources["VsBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));
            resources["VsTextBrush"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
            resources["VsMutedTextBrush"] = new SolidColorBrush(Color.FromRgb(0x9D, 0xA1, 0xA6));
            resources["VsAccentBrush"] = new SolidColorBrush(accent);
            return;
        }

        resources["VsWindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        resources["VsTitleBarBrush"] = new SolidColorBrush(Color.FromRgb(0xE7, 0xE7, 0xE7));
        resources["VsPanelBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["VsPanelAltBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8));
        resources["VsBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
        resources["VsTextBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        resources["VsMutedTextBrush"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x63, 0x6A));
        resources["VsAccentBrush"] = new SolidColorBrush(accent);
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            var value = hex.Trim();
            if (value[0] == '#')
            {
                value = value[1..];
            }

            if (value.Length == 6)
            {
                return Color.FromRgb(
                    (byte)Convert.ToInt32(value.Substring(0, 2), 16),
                    (byte)Convert.ToInt32(value.Substring(2, 2), 16),
                    (byte)Convert.ToInt32(value.Substring(4, 2), 16));
            }

            if (value.Length == 8)
            {
                return Color.FromArgb(
                    (byte)Convert.ToInt32(value.Substring(0, 2), 16),
                    (byte)Convert.ToInt32(value.Substring(2, 2), 16),
                    (byte)Convert.ToInt32(value.Substring(4, 2), 16),
                    (byte)Convert.ToInt32(value.Substring(6, 2), 16));
            }
        }
        catch
        {
        }

        return null;
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
