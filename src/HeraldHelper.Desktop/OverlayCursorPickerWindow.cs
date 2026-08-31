using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace HeraldHelper.Desktop;

public sealed class OverlayCursorPickerWindow : Window
{
    private readonly TextBlock _hint;
    private readonly Ellipse _dot;
    private readonly Canvas _canvas;
    private readonly bool _hideHint;
    public event Action<int, int>? PositionChanged;
    public (int X, int Y)? SelectedPosition { get; private set; }

    public OverlayCursorPickerWindow(bool hideHint = false)
    {
        _hideHint = hideHint;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(WpfColor.FromArgb(1, 0, 0, 0));
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Cursor = System.Windows.Input.Cursors.Cross;

        _canvas = new Canvas();
        _dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = WpfBrushes.White,
            Stroke = WpfBrushes.Black,
            StrokeThickness = 1.5
        };
        _hint = new TextBlock
        {
            Text = "Move mouse to place overlay. Left click to confirm, Esc to cancel.",
            Foreground = WpfBrushes.White,
            Background = new SolidColorBrush(WpfColor.FromArgb(180, 30, 30, 30)),
            Padding = new Thickness(10, 6, 10, 6),
            Visibility = _hideHint ? Visibility.Collapsed : Visibility.Visible
        };

        _canvas.Children.Add(_dot);
        _canvas.Children.Add(_hint);
        Canvas.SetLeft(_hint, 20);
        Canvas.SetTop(_hint, 20);
        Content = _canvas;

        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var local = e.GetPosition(this);
        Canvas.SetLeft(_dot, local.X - (_dot.Width / 2));
        Canvas.SetTop(_dot, local.Y - (_dot.Height / 2));

        var screen = PointToScreen(local);
        var x = (int)Math.Round(screen.X);
        var y = (int)Math.Round(screen.Y);
        PositionChanged?.Invoke(x, y);
    }

    private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var local = e.GetPosition(this);
        var screen = PointToScreen(local);
        SelectedPosition = ((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
        DialogResult = true;
        Close();
    }

    public static (int X, int Y)? Pick(Window owner, Action<int, int>? onMove = null)
    {
        var picker = new OverlayCursorPickerWindow
        {
            Owner = owner
        };

        if (onMove is not null)
        {
            picker.PositionChanged += onMove;
        }

        var ok = picker.ShowDialog();
        return ok == true ? picker.SelectedPosition : null;
    }
}
