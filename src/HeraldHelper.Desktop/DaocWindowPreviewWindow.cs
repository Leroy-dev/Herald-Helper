using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace HeraldHelper.Desktop;

public sealed class DaocWindowPreviewWindow : Window
{
    private readonly Canvas _canvas;
    private readonly TextBlock _hint;
    private double _originLeft;
    private double _originTop;

    public DaocWindowPreviewWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = new SolidColorBrush(WpfColor.FromArgb(1, 0, 0, 0));
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Left = SystemParameters.WorkArea.Left;
        Top = SystemParameters.WorkArea.Top;
        Width = SystemParameters.WorkArea.Width;
        Height = SystemParameters.WorkArea.Height;

        _canvas = new Canvas();
        _hint = new TextBlock
        {
            Text = "OCR preview",
            Foreground = WpfBrushes.White,
            Background = new SolidColorBrush(WpfColor.FromArgb(180, 30, 30, 30)),
            Padding = new Thickness(10, 6, 10, 6)
        };
        Canvas.SetLeft(_hint, 20);
        Canvas.SetTop(_hint, 20);
        _canvas.Children.Add(_hint);
        Content = _canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        exStyle |= OverlayTextWindowInterop.WsExLayered | OverlayTextWindowInterop.WsExTransparent | OverlayTextWindowInterop.WsExNoActivate | OverlayTextWindowInterop.WsExToolWindow;
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
    }

    public void UpdateWindows(IReadOnlyCollection<DaocWindowDefinition> windows, IReadOnlyCollection<DaocWindowDefinition>? layoutWindows = null)
    {
        _canvas.Children.Clear();
        _canvas.Children.Add(_hint);

        layoutWindows ??= windows;
        if (layoutWindows.Count > 0)
        {
            _originLeft = layoutWindows.Min(x => x.Region.X);
            _originTop = layoutWindows.Min(x => x.Region.Y);
            var right = layoutWindows.Max(x => x.Region.X + x.Region.Width);
            var bottom = layoutWindows.Max(x => x.Region.Y + x.Region.Height);

            Left = _originLeft;
            Top = _originTop;
            Width = Math.Max(320, right - _originLeft);
            Height = Math.Max(240, bottom - _originTop);
        }
        else
        {
            _originLeft = Left;
            _originTop = Top;
        }

        var fills = new[]
        {
            WpfColor.FromArgb(60, 0, 122, 204),
            WpfColor.FromArgb(60, 255, 140, 0),
            WpfColor.FromArgb(60, 144, 238, 144),
            WpfColor.FromArgb(60, 220, 20, 60)
        };

        var index = 0;
        foreach (var window in windows)
        {
            var fill = new SolidColorBrush(fills[index % fills.Length]);
            var stroke = new SolidColorBrush(WpfColor.FromArgb(220, fill.Color.R, fill.Color.G, fill.Color.B));
            var rect = new WpfRectangle
            {
                Width = Math.Max(1, window.Region.Width),
                Height = Math.Max(1, window.Region.Height),
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = 2,
                RadiusX = 3,
                RadiusY = 3
            };

            var left = window.Region.X - _originLeft;
            var top = window.Region.Y - _originTop;
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            _canvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = window.Label,
                Foreground = WpfBrushes.White,
                Background = new SolidColorBrush(WpfColor.FromArgb(200, 20, 20, 20)),
                Padding = new Thickness(6, 2, 6, 2)
            };
            Canvas.SetLeft(label, left + 4);
            Canvas.SetTop(label, Math.Max(0, top - 22));
            _canvas.Children.Add(label);
            index++;
        }

        if (!IsVisible)
        {
            Show();
        }
    }
}
