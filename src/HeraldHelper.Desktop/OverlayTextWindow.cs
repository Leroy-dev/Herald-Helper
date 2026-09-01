using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public sealed class OverlayTextWindow : Window
{
    private readonly TextBlock _mainTextBlock;
    private readonly TextBlock[] _outlineTextBlocks;

    public OverlayTextWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        var root = new Grid
        {
            Margin = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent
        };

        _outlineTextBlocks =
        [
            CreateOutlineText(-1, -1),
            CreateOutlineText(-1, 0),
            CreateOutlineText(-1, 1),
            CreateOutlineText(0, -1),
            CreateOutlineText(0, 1),
            CreateOutlineText(1, -1),
            CreateOutlineText(1, 0),
            CreateOutlineText(1, 1)
        ];

        foreach (var outline in _outlineTextBlocks)
        {
            root.Children.Add(outline);
        }

        _mainTextBlock = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 20,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
            TextWrapping = TextWrapping.NoWrap,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 2,
                ShadowDepth = 0,
                Opacity = 0.9
            }
        };

        root.Children.Add(_mainTextBlock);
        Content = root;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        exStyle |= OverlayTextWindowInterop.WsExLayered | OverlayTextWindowInterop.WsExTransparent | OverlayTextWindowInterop.WsExNoActivate | OverlayTextWindowInterop.WsExToolWindow;
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
    }

    public void Update(string text, double x, double y, double fontSize, string fontFamily, MediaColor foregroundColor, MediaColor outlineColor, FontWeight? fontWeight = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Hide();
            return;
        }

        var family = new System.Windows.Media.FontFamily(fontFamily);

        _mainTextBlock.Text = text;
        _mainTextBlock.FontSize = fontSize;
        _mainTextBlock.FontFamily = family;
        if (fontWeight is not null)
        {
            _mainTextBlock.FontWeight = fontWeight.Value;
        }
        _mainTextBlock.Foreground = new SolidColorBrush(foregroundColor);
        if (_mainTextBlock.Effect is DropShadowEffect shadow)
        {
            shadow.Color = outlineColor;
        }

        foreach (var outline in _outlineTextBlocks)
        {
            outline.Text = text;
            outline.FontSize = fontSize;
            outline.FontFamily = family;
            if (fontWeight is not null)
            {
                outline.FontWeight = fontWeight.Value;
            }
            outline.Foreground = new SolidColorBrush(outlineColor);
        }

        Left = x;
        Top = y;
        if (!IsVisible)
        {
            Show();
        }
    }

    private static TextBlock CreateOutlineText(double offsetX, double offsetY)
    {
        return new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.Black,
            FontSize = 20,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(offsetX, offsetY, 0, 0)
        };
    }
}
