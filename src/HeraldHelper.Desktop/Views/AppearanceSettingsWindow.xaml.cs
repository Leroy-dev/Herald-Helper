using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using Color = System.Windows.Media.Color;

namespace HeraldHelper.Desktop.Views;

public partial class AppearanceSettingsWindow : Window
{
    private readonly ThemeController _themeController;
    private Color _accent;
    private ApplicationTheme _mode;

    public AppearanceSettingsWindow(ThemeController themeController)
    {
        _themeController = themeController;
        _accent = themeController.Accent;
        _mode = themeController.Mode;
        InitializeComponent();
        ThemeCombo.SelectedIndex = (int)_mode;
        CustomColorText.Text = ToHex(_accent);
        UpdatePreview();
    }

    private void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string hex)
        {
            return;
        }

        var color = TryParseColor(hex);
        if (color is null)
        {
            return;
        }

        _accent = color.Value;
        CustomColorText.Text = hex;
        UpdatePreview();
    }

    private void CustomColorText_TextChanged(object sender, TextChangedEventArgs e)
    {
        var color = TryParseColor(CustomColorText.Text);
        if (color is null)
        {
            return;
        }

        _accent = color.Value;
        UpdatePreview();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _mode = ApplicationTheme.Dark;
        _accent = Color.FromRgb(0x00, 0x7A, 0xCC);
        ThemeCombo.SelectedIndex = (int)_mode;
        CustomColorText.Text = ToHex(_accent);
        UpdatePreview();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _mode = tag.ToLowerInvariant() switch
            {
                "light" => ApplicationTheme.Light,
                "system" => ApplicationTheme.System,
                _ => ApplicationTheme.Dark
            };
        }

        _themeController.Save(_mode, _accent);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdatePreview()
    {
        ColorPreview.Background = new SolidColorBrush(_accent);
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
