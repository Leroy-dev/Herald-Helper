using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HeraldHelper.Domain.Models;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public sealed class OverlayTextWindow : Window
{
    private readonly TextBlock _mainTextBlock;
    private readonly TextBlock[] _outlineTextBlocks;
    private readonly IconImageLoader _iconLoader = new();

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

        _mainTextBlock.Inlines.Clear();
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
            outline.Inlines.Clear();
            outline.Text = text;
            outline.FontSize = fontSize;
            outline.FontFamily = family;
            if (fontWeight is not null)
            {
                outline.FontWeight = fontWeight.Value;
            }
            outline.Foreground = new SolidColorBrush(outlineColor);
        }

        ClampToScreen(x, y);
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>Multi-color variant: each entry becomes a line in its own
    /// color, optionally preceded by the ability's catalog icon. Outline
    /// copies get a blank spacer of the icon width so the halo stays aligned
    /// under the icon-offset text.</summary>
    public void Update(IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)> lines, double x, double y, double fontSize, string fontFamily, MediaColor outlineColor)
    {
        // Icon-only lines are legit — the pet window's effect icons have no text.
        var visible = lines.Where(l => !string.IsNullOrWhiteSpace(l.Text) || l.Icon is not null).ToList();
        if (visible.Count == 0)
        {
            Hide();
            return;
        }

        var family = new System.Windows.Media.FontFamily(fontFamily);
        var iconSize = fontSize * 0.9;
        _mainTextBlock.Inlines.Clear();
        _mainTextBlock.FontSize = fontSize;
        _mainTextBlock.FontFamily = family;
        _mainTextBlock.FontWeight = FontWeights.SemiBold;
        if (_mainTextBlock.Effect is DropShadowEffect shadow)
        {
            shadow.Color = outlineColor;
        }

        for (var i = 0; i < visible.Count; i++)
        {
            if (i > 0)
            {
                _mainTextBlock.Inlines.Add(new LineBreak());
            }

            var iconImage = visible[i].Icon is { } iconRef ? _iconLoader.Load(iconRef) : null;
            if (iconImage is not null)
            {
                _mainTextBlock.Inlines.Add(new InlineUIContainer(
                    new System.Windows.Controls.Image
                    {
                        Source = iconImage,
                        Width = iconSize,
                        Height = iconSize,
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    }));
            }

            _mainTextBlock.Inlines.Add(new Run(visible[i].Text) { Foreground = new SolidColorBrush(visible[i].Color) });
        }

        foreach (var outline in _outlineTextBlocks)
        {
            outline.Inlines.Clear();
            outline.FontSize = fontSize;
            outline.FontFamily = family;
            outline.FontWeight = FontWeights.SemiBold;
            outline.Foreground = new SolidColorBrush(outlineColor);

            for (var i = 0; i < visible.Count; i++)
            {
                if (i > 0)
                {
                    outline.Inlines.Add(new LineBreak());
                }

                if (visible[i].Icon is not null)
                {
                    outline.Inlines.Add(new InlineUIContainer(
                        new Border { Width = iconSize + 4, Height = iconSize }));
                }

                outline.Inlines.Add(new Run(visible[i].Text));
            }
        }

        ClampToScreen(x, y);
        if (!IsVisible)
        {
            Show();
        }
    }

    private void ClampToScreen(double x, double y)
    {
        // Saved/preview positions can sit past the visible screen (smaller
        // game resolution, VM, monitor rearranged) — keep a slice on-screen
        // so the overlay can't render invisibly.
        Left = Math.Clamp(x,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 40);
        Top = Math.Clamp(y,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 20);
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
