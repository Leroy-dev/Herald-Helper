using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;

namespace HeraldHelper.Desktop;

public sealed class LiveColorPickerWindow : Window
{
    private readonly Slider _r;
    private readonly Slider _g;
    private readonly Slider _b;
    private readonly Border _preview;
    private readonly WpfTextBox _hex;
    private readonly Action<string>? _onColorChanged;

    public string SelectedHex { get; private set; }

    public LiveColorPickerWindow(string initialHex, Action<string>? onColorChanged = null)
    {
        _onColorChanged = onColorChanged;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 360;
        Height = 280;
        ResizeMode = ResizeMode.NoResize;
        Title = "Pick Overlay Color";
        Background = new SolidColorBrush(WpfColor.FromRgb(32, 32, 32));

        var color = ParseHex(initialHex);
        SelectedHex = ToHex(color);

        var panel = new StackPanel { Margin = new Thickness(12) };
        _preview = new Border
        {
            Height = 42,
            Background = new SolidColorBrush(color),
            BorderBrush = WpfBrushes.White,
            BorderThickness = new Thickness(1)
        };
        _hex = new WpfTextBox
        {
            Margin = new Thickness(0, 8, 0, 8),
            Text = SelectedHex,
            IsReadOnly = true,
            Foreground = WpfBrushes.White,
            Background = new SolidColorBrush(WpfColor.FromRgb(45, 45, 45))
        };

        _r = BuildSlider("R", color.R, panel);
        _g = BuildSlider("G", color.G, panel);
        _b = BuildSlider("B", color.B, panel);

        panel.Children.Insert(0, _hex);
        panel.Children.Insert(0, _preview);

        var buttons = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new WpfButton { Content = "OK", MinWidth = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new WpfButton { Content = "Cancel", MinWidth = 70 };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;

        _r.ValueChanged += (_, _) => ApplyLiveColor();
        _g.ValueChanged += (_, _) => ApplyLiveColor();
        _b.ValueChanged += (_, _) => ApplyLiveColor();
    }

    private static Slider BuildSlider(string label, byte value, WpfPanel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 6, 0, 2)
        });
        var s = new Slider
        {
            Minimum = 0,
            Maximum = 255,
            Value = value
        };
        panel.Children.Add(s);
        return s;
    }

    private void ApplyLiveColor()
    {
        var color = WpfColor.FromRgb((byte)_r.Value, (byte)_g.Value, (byte)_b.Value);
        SelectedHex = ToHex(color);
        _preview.Background = new SolidColorBrush(color);
        _hex.Text = SelectedHex;
        _onColorChanged?.Invoke(SelectedHex);
    }

    private static WpfColor ParseHex(string raw)
    {
        try
        {
            var converted = System.Windows.Media.ColorConverter.ConvertFromString(raw);
            if (converted is WpfColor c)
            {
                return c;
            }
        }
        catch
        {
        }

        return System.Windows.Media.Colors.White;
    }

    private static string ToHex(WpfColor c)
    {
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    public static string? Pick(Window owner, string initialHex, Action<string>? onColorChanged = null)
    {
        var dialog = new LiveColorPickerWindow(initialHex, onColorChanged)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.SelectedHex : null;
    }
}
