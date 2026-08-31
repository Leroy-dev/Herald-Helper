using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class ThemeController
{
    private readonly PaletteHelper _paletteHelper = new();
    private readonly SettingsController _settingsController;

    public ThemeController(SettingsController settingsController)
    {
        _settingsController = settingsController;
    }

    public bool IsDark { get; private set; } = true;

    public void Initialize()
    {
        var settings = _settingsController.LoadMap();
        var theme = settings.TryGetValue("ui.theme", out var themeRaw) ? themeRaw : "dark";
        IsDark = !string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        Apply(IsDark);
    }

    public void Toggle()
    {
        IsDark = !IsDark;
        Apply(IsDark);
        _settingsController.Save([
            new ConfigEntry { Key = "ui.theme", Value = IsDark ? "dark" : "light" }
        ]);
    }

    public void Apply(bool dark)
    {
        IsDark = dark;
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
        ApplyVsCodePalette(dark);
    }

    public string ToggleButtonContent => IsDark ? "Switch to Light" : "Switch to Dark";

    private static void ApplyVsCodePalette(bool dark)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        var resources = System.Windows.Application.Current.Resources;
        if (dark)
        {
            resources["VsWindowBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["VsTitleBarBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelAltBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
            resources["VsBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x42));
            resources["VsTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
            resources["VsMutedTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9D, 0xA1, 0xA6));
            resources["VsAccentBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
            return;
        }

        resources["VsWindowBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
        resources["VsTitleBarBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE7, 0xE7, 0xE7));
        resources["VsPanelBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["VsPanelAltBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xF8, 0xF8));
        resources["VsBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
        resources["VsTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        resources["VsMutedTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5C, 0x63, 0x6A));
        resources["VsAccentBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x6B, 0xC1));
    }
}
