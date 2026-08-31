using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HeraldHelper.Domain.Models;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace HeraldHelper.Desktop;

public sealed class CastBarWindow : Window
{
    private readonly IconImageLoader _iconLoader = new();
    private readonly Image _iconImage;
    private readonly TextBlock _spellNameText;
    private readonly TextBlock _secondsText;
    private readonly ProgressBar _progressBar;
    private readonly Border _border;
    private readonly DispatcherTimer _timer;
    private CastBarState? _state;
    private MediaColor _targetColor = Colors.White;
    private MediaColor _timerColor = MediaColor.FromRgb(255, 233, 178);
    private MediaColor _outlineColor = Colors.Black;

    public CastBarWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Width = 280;
        Height = 48;

        _iconImage = new Image
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Fill
        };

        _spellNameText = new TextBlock
        {
            Foreground = new SolidColorBrush(_targetColor),
            FontSize = 15,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Effect = new DropShadowEffect
            {
                Color = _outlineColor,
                BlurRadius = 2,
                ShadowDepth = 0,
                Opacity = 0.9
            }
        };

        _secondsText = new TextBlock
        {
            Foreground = new SolidColorBrush(_timerColor),
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 4,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(_timerColor),
            Background = new SolidColorBrush(MediaColor.FromArgb(120, 0, 0, 0)),
            BorderThickness = new Thickness(0)
        };

        var textGrid = new Grid();
        textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        textGrid.Children.Add(_spellNameText);
        textGrid.Children.Add(_secondsText);
        Grid.SetRow(_secondsText, 1);

        var infoPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
        infoPanel.Children.Add(textGrid);
        infoPanel.Children.Add(_progressBar);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(_iconImage);
        content.Children.Add(infoPanel);
        Grid.SetColumn(infoPanel, 1);

        _border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(MediaColor.FromArgb(180, 20, 24, 31)),
            BorderBrush = new SolidColorBrush(_outlineColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = content
        };

        Content = _border;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += (_, _) => Refresh();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        exStyle |= OverlayTextWindowInterop.WsExLayered | OverlayTextWindowInterop.WsExTransparent | OverlayTextWindowInterop.WsExNoActivate | OverlayTextWindowInterop.WsExToolWindow;
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
    }

    public void Update(CastBarState? state, double x, double y, MediaColor targetColor, MediaColor timerColor, MediaColor outlineColor)
    {
        _state = state;
        Left = x;
        Top = y;

        if (_targetColor != targetColor || _outlineColor != outlineColor)
        {
            _targetColor = targetColor;
            _outlineColor = outlineColor;
            _spellNameText.Foreground = new SolidColorBrush(targetColor);
            _spellNameText.Effect = new DropShadowEffect
            {
                Color = outlineColor,
                BlurRadius = 2,
                ShadowDepth = 0,
                Opacity = 0.9
            };
            _border.BorderBrush = new SolidColorBrush(outlineColor);
        }

        if (_timerColor != timerColor)
        {
            _timerColor = timerColor;
            _secondsText.Foreground = new SolidColorBrush(timerColor);
            _progressBar.Foreground = new SolidColorBrush(timerColor);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_state is null || !_state.IsActive(DateTimeOffset.UtcNow))
        {
            _timer.Stop();
            if (IsVisible)
            {
                Hide();
            }
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        _spellNameText.Text = _state.SpellName;
        var damage = _state.EstimatedDamage is > 0
            ? $" · ~{_state.EstimatedDamage:0} dmg"
            : string.Empty;
        _secondsText.Text = $"{_state.RemainingSeconds(nowUtc):0.0}s{damage}";
        _progressBar.Value = _state.Progress(nowUtc);
        _iconImage.Source = ResolveImage(_state.Icon);
        _iconImage.Visibility = _iconImage.Source is null ? Visibility.Collapsed : Visibility.Visible;

        if (!IsVisible)
        {
            Show();
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private ImageSource? ResolveImage(IconSpriteRef? icon)
    {
        return _iconLoader.Load(icon);
    }
}
