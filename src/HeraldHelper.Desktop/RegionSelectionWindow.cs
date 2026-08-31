using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HeraldHelper.Domain.Models;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace HeraldHelper.Desktop;

public sealed class RegionSelectionWindow : Window
{
    private readonly System.Windows.Controls.Canvas _canvas;
    private readonly WpfRectangle _selectionRect;
    private WpfPoint? _dragStart;

    public ScreenRegion? SelectedRegion { get; private set; }

    public RegionSelectionWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Cursor = System.Windows.Input.Cursors.Cross;

        _canvas = new System.Windows.Controls.Canvas();
        _selectionRect = new WpfRectangle
        {
            Stroke = System.Windows.Media.Brushes.Red,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 0, 0)),
            Visibility = Visibility.Collapsed
        };
        _canvas.Children.Add(_selectionRect);
        Content = _canvas;

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    public static ScreenRegion? Select(Window owner)
    {
        var selector = new RegionSelectionWindow { Owner = owner };
        var ok = selector.ShowDialog();
        return ok == true ? selector.SelectedRegion : null;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _selectionRect.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var x = Math.Min(_dragStart.Value.X, current.X);
        var y = Math.Min(_dragStart.Value.Y, current.Y);
        var w = Math.Abs(current.X - _dragStart.Value.X);
        var h = Math.Abs(current.Y - _dragStart.Value.Y);

        System.Windows.Controls.Canvas.SetLeft(_selectionRect, x);
        System.Windows.Controls.Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var end = e.GetPosition(this);
        ReleaseMouseCapture();

        var startScreen = PointToScreen(_dragStart.Value);
        var endScreen = PointToScreen(end);

        var x = (int)Math.Round(Math.Min(startScreen.X, endScreen.X));
        var y = (int)Math.Round(Math.Min(startScreen.Y, endScreen.Y));
        var w = (int)Math.Round(Math.Abs(endScreen.X - startScreen.X));
        var h = (int)Math.Round(Math.Abs(endScreen.Y - startScreen.Y));

        if (w >= 4 && h >= 4)
        {
            SelectedRegion = new ScreenRegion(x, y, w, h);
            DialogResult = true;
        }
        else
        {
            DialogResult = false;
        }

        Close();
    }
}
