using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HeraldHelper.Desktop.Models;

namespace HeraldHelper.Desktop.Views;

public partial class LiveView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public LiveView()
    {
        InitializeComponent();
    }

    private void ClearResponseDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Main?.ClearResponseDiagnostics_Click(sender, e);
    }

    /// <summary>
    /// Mirrors the last rendered overlay state into the Live panel — same
    /// text, fonts and colors the on-screen overlay windows just drew.
    /// </summary>
    public void UpdateMirror(OverlayViewState state)
    {
        var hasContent = !string.IsNullOrWhiteSpace(state.TargetText) ||
                         !string.IsNullOrWhiteSpace(state.TimerText) ||
                         state.Cast is not null;
        MirrorEmptyText.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;

        MirrorTargetText.Text = state.TargetText;
        MirrorTargetText.Foreground = new SolidColorBrush(state.TargetColor);
        MirrorTargetText.FontFamily = new System.Windows.Media.FontFamily(state.TargetFontFamily);
        MirrorTargetText.FontSize = Math.Clamp(state.TargetFontSize, 11, 22);

        MirrorTimerText.Text = state.TimerText;
        MirrorTimerText.Foreground = new SolidColorBrush(state.TimerColor);
        MirrorTimerText.FontFamily = new System.Windows.Media.FontFamily(state.TimerFontFamily);
        MirrorTimerText.FontSize = Math.Clamp(state.TimerFontSize, 10, 18);

        if (state.Cast is { } cast && cast.IsActive(state.RenderedAtUtc))
        {
            MirrorCastPanel.Visibility = Visibility.Visible;
            MirrorCastText.Text = cast.SpellName;
            MirrorCastText.Foreground = new SolidColorBrush(state.TargetColor);
            MirrorCastFill.Background = new SolidColorBrush(state.TimerColor);

            var trackWidth = MirrorCastPanel.ActualWidth;
            if (trackWidth > 0)
            {
                MirrorCastFill.Width = trackWidth * cast.Progress(state.RenderedAtUtc);
            }
        }
        else
        {
            MirrorCastPanel.Visibility = Visibility.Collapsed;
        }
    }
}
