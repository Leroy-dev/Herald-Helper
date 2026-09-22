using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using HeraldHelper.Domain.Models;
using FontFamily = System.Windows.Media.FontFamily;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

/// <summary>
/// Group frames — one row per group member: name+class, HP bar, End/Pow
/// thin bars. Data comes from the client's group_* adapter values; the
/// window stays hidden until a group actually populates.
/// </summary>
public sealed class GroupOverlayWindow : Window
{
    private const int MaxRows = 8;
    private const double BarWidth = 168;
    private readonly StackPanel _panel;
    private readonly List<MemberRow> _rows = new(MaxRows);

    public GroupOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Width = 190;
        SizeToContent = SizeToContent.Height;

        _panel = new StackPanel();
        for (var i = 0; i < MaxRows; i++)
        {
            var row = new MemberRow();
            row.Root.Visibility = Visibility.Collapsed;
            _rows.Add(row);
            _panel.Children.Add(row.Root);
        }

        Content = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(MediaColor.FromArgb(180, 20, 24, 31)),
            BorderBrush = new SolidColorBrush(Colors.Black),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Child = _panel
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = OverlayTextWindowInterop.GetWindowLongPtr(hwnd).ToInt64();
        exStyle |= OverlayTextWindowInterop.WsExLayered | OverlayTextWindowInterop.WsExTransparent | OverlayTextWindowInterop.WsExNoActivate | OverlayTextWindowInterop.WsExToolWindow;
        OverlayTextWindowInterop.SetWindowLongPtr(hwnd, new IntPtr(exStyle));
    }

    public void Update(
        IReadOnlyList<GroupMemberState>? members,
        double x,
        double y,
        int fontSize,
        string fontFamily,
        MediaColor outlineColor)
    {
        if (members is null || members.Count == 0)
        {
            if (IsVisible)
            {
                Hide();
            }
            return;
        }

        Left = Math.Clamp(x,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width - 20);
        Top = Math.Clamp(y,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40);

        var family = new FontFamily(fontFamily);
        var shown = Math.Min(members.Count, MaxRows);
        for (var i = 0; i < shown; i++)
        {
            _rows[i].Update(members[i], fontSize, family);
            _rows[i].Root.Visibility = Visibility.Visible;
        }
        for (var i = shown; i < MaxRows; i++)
        {
            _rows[i].Root.Visibility = Visibility.Collapsed;
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    private sealed class MemberRow
    {
        public readonly Border Root;
        private readonly TextBlock _nameText;
        private readonly TextBlock _zoneText;
        private readonly Border _hpFill;
        private readonly TextBlock _hpText;
        private readonly Border _endFill;
        private readonly Border _powFill;

        public MemberRow()
        {
            _nameText = MakeText(13, FontWeights.SemiBold, Colors.White);
            _zoneText = MakeText(10, FontWeights.Normal, MediaColor.FromRgb(0x9D, 0xA1, 0xA6));
            _zoneText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            _zoneText.TextTrimming = TextTrimming.CharacterEllipsis;
            _zoneText.MaxWidth = 90;

            var nameGrid = new Grid();
            nameGrid.Children.Add(_nameText);
            nameGrid.Children.Add(_zoneText);

            var hpGrid = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            var hpContainer = new Border
            {
                Height = 10,
                Background = new SolidColorBrush(MediaColor.FromArgb(140, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                ClipToBounds = true
            };
            _hpFill = new Border
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = 0,
                CornerRadius = new CornerRadius(2)
            };
            hpContainer.Child = _hpFill;
            _hpText = MakeText(9, FontWeights.Bold, Colors.White);
            _hpText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            _hpText.VerticalAlignment = VerticalAlignment.Center;
            _hpText.Margin = new Thickness(0, 0, 3, 0);
            _hpText.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 1,
                ShadowDepth = 0,
                Opacity = 1
            };
            hpGrid.Children.Add(hpContainer);
            hpGrid.Children.Add(_hpText);

            var vitalsGrid = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            vitalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vitalsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var endContainer = new Border
            {
                Height = 4,
                Margin = new Thickness(0, 0, 1, 0),
                Background = new SolidColorBrush(MediaColor.FromArgb(140, 0, 0, 0)),
                CornerRadius = new CornerRadius(1),
                ClipToBounds = true
            };
            _endFill = new Border
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = 0,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(MediaColor.FromRgb(0xE7, 0xC2, 0x4A))
            };
            endContainer.Child = _endFill;
            var powContainer = new Border
            {
                Height = 4,
                Margin = new Thickness(1, 0, 0, 0),
                Background = new SolidColorBrush(MediaColor.FromArgb(140, 0, 0, 0)),
                CornerRadius = new CornerRadius(1),
                ClipToBounds = true
            };
            _powFill = new Border
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = 0,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(MediaColor.FromRgb(0x4A, 0x9F, 0xE7))
            };
            powContainer.Child = _powFill;
            vitalsGrid.Children.Add(endContainer);
            vitalsGrid.Children.Add(powContainer);
            Grid.SetColumn(powContainer, 1);

            var stack = new StackPanel();
            stack.Children.Add(nameGrid);
            stack.Children.Add(hpGrid);
            stack.Children.Add(vitalsGrid);

            Root = new Border
            {
                Padding = new Thickness(0, 2, 0, 2),
                Child = stack
            };
        }

        public void Update(GroupMemberState member, int fontSize, FontFamily family)
        {
            _nameText.FontSize = fontSize;
            _nameText.FontFamily = family;
            _nameText.Text = string.IsNullOrWhiteSpace(member.Class)
                ? member.Name
                : $"{member.Name} ·{member.Class[..Math.Min(3, member.Class.Length)]}";

            _zoneText.Text = member.Zone ?? string.Empty;
            _zoneText.Visibility = string.IsNullOrWhiteSpace(member.Zone)
                ? Visibility.Collapsed
                : Visibility.Visible;

            var hp = member.HealthPercent;
            _hpFill.Width = hp is null ? 0 : BarWidth * Math.Clamp(hp.Value, 0, 100) / 100.0;
            _hpFill.Background = new SolidColorBrush(HpColor(hp));
            _hpText.Text = hp?.ToString() ?? "?";

            _endFill.Width = member.EndurancePercent is { } end
                ? (BarWidth / 2 - 1) * Math.Clamp(end, 0, 100) / 100.0
                : 0;
            _powFill.Width = member.PowerPercent is { } pow
                ? (BarWidth / 2 - 1) * Math.Clamp(pow, 0, 100) / 100.0
                : 0;
        }

        private static MediaColor HpColor(int? percent) => percent switch
        {
            null => MediaColor.FromRgb(0x55, 0x5B, 0x66),
            < 35 => MediaColor.FromRgb(0xE0, 0x5D, 0x65),
            < 70 => MediaColor.FromRgb(0xE7, 0xA9, 0x3A),
            _ => MediaColor.FromRgb(0x45, 0xB9, 0x7C)
        };

        private static TextBlock MakeText(double size, FontWeight weight, MediaColor color) => new()
        {
            FontSize = size,
            FontWeight = weight,
            Foreground = new SolidColorBrush(color),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }
}
