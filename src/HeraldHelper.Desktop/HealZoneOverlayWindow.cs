using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;

namespace HeraldHelper.Desktop;

/// <summary>
/// Click-zone overlay parked over the in-game heal window: invisible row
/// strips that capture clicks without stealing focus (WS_EX_NOACTIVATE),
/// passing through everywhere else via a WM_NCHITTEST gate. Row index is the
/// group slot — the in-game window's rows are fixed SelectGroupMemberN zones,
/// so no memory read is needed.
/// </summary>
public sealed class HealZoneOverlayWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtTransparent = -1;

    private readonly StackPanel _panel;
    private readonly List<Border> _zones = [];
    private bool _clickable;
    private int _rows = 8;
    private double _rowHeight = 26;

    /// <summary>Zone index clicked — only fires while click mode is on.</summary>
    public event Action<int>? ZoneClicked;

    public HealZoneOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;

        _panel = new StackPanel();
        Content = _panel;
        RebuildZones();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        exStyle |= OverlayTextWindowInterop.WsExLayered | OverlayTextWindowInterop.WsExTransparent | OverlayTextWindowInterop.WsExNoActivate | OverlayTextWindowInterop.WsExToolWindow;
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        source?.AddHook(WndProc);
    }

    /// <summary>Configure the zone geometry and park the window over the
    /// in-game heal window. debugPaint draws faint zones for alignment.</summary>
    public void SetRegion(double x, double y, double width, int rows, double rowHeight, bool debugPaint)
    {
        Left = x;
        Top = y;
        Width = width;
        Height = rows * rowHeight;
        _rows = Math.Clamp(rows, 1, 32);
        _rowHeight = Math.Max(4, rowHeight);
        RebuildZones(debugPaint);
        if (!IsVisible)
        {
            Show();
        }
    }

    public void SetClickable(bool enabled)
    {
        if (enabled == _clickable)
        {
            return;
        }
        _clickable = enabled;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        if (enabled)
        {
            exStyle &= ~OverlayTextWindowInterop.WsExTransparent;
        }
        else
        {
            exStyle |= OverlayTextWindowInterop.WsExTransparent;
        }
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
        IsHitTestVisible = enabled;
    }

    public void HideZone()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private void RebuildZones(bool debugPaint = false)
    {
        _panel.Children.Clear();
        _zones.Clear();
        for (var i = 0; i < _rows; i++)
        {
            var zone = new Border
            {
                Height = _rowHeight,
                Background = debugPaint
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0xE0, 0x8C, 0x3C))
                    : MediaBrushes.Transparent,
                BorderBrush = debugPaint
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 0xE0, 0x8C, 0x3C))
                    : null,
                BorderThickness = debugPaint ? new Thickness(1) : new Thickness(0)
            };
            var index = i;
            zone.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                ZoneClicked?.Invoke(index);
            };
            _zones.Add(zone);
            _panel.Children.Add(zone);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmNcHitTest)
        {
            var passThrough = new IntPtr(HtTransparent);
            if (!_clickable)
            {
                handled = true;
                return passThrough;
            }
            var screenX = (short)(lParam.ToInt32() & 0xFFFF);
            var screenY = (short)(lParam.ToInt32() >> 16);
            try
            {
                var client = PointFromScreen(new System.Windows.Point(screenX, screenY));
                var origin = _panel.TranslatePoint(new System.Windows.Point(0, 0), this);
                var rows = new Rect(origin, _panel.RenderSize);
                handled = true;
                return rows.Contains(client) ? new IntPtr(HtClient) : passThrough;
            }
            catch (InvalidOperationException)
            {
                handled = true;
                return passThrough;
            }
        }
        return IntPtr.Zero;
    }
}
