using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;

namespace HeraldHelper.Desktop;

public sealed class LiveSizePickerWindow : Window
{
    private readonly Slider _slider;
    private readonly TextBlock _valueText;
    private readonly Action<int>? _onSizeChanged;
    public int SelectedSize { get; private set; }

    public LiveSizePickerWindow(string label, int initialSize, Action<int>? onSizeChanged = null)
    {
        _onSizeChanged = onSizeChanged;
        SelectedSize = Math.Clamp(initialSize, 10, 72);

        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 340;
        Height = 180;
        ResizeMode = ResizeMode.NoResize;
        Title = $"Resize {label}";
        Background = new SolidColorBrush(WpfColor.FromRgb(32, 32, 32));

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock
        {
            Text = $"{label} font size",
            Foreground = WpfBrushes.White
        });

        _slider = new Slider
        {
            Margin = new Thickness(0, 12, 0, 6),
            Minimum = 10,
            Maximum = 72,
            Value = SelectedSize
        };
        _valueText = new TextBlock
        {
            Foreground = WpfBrushes.White,
            Text = $"Size: {SelectedSize}"
        };

        panel.Children.Add(_slider);
        panel.Children.Add(_valueText);

        var buttons = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new WpfButton { Content = "OK", MinWidth = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new WpfButton { Content = "Cancel", MinWidth = 70 };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;

        _slider.ValueChanged += (_, _) =>
        {
            SelectedSize = (int)Math.Round(_slider.Value);
            _valueText.Text = $"Size: {SelectedSize}";
            _onSizeChanged?.Invoke(SelectedSize);
        };
    }

    public static int? Pick(Window owner, string label, int initialSize, Action<int>? onSizeChanged = null)
    {
        var dialog = new LiveSizePickerWindow(label, initialSize, onSizeChanged)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.SelectedSize : null;
    }
}
