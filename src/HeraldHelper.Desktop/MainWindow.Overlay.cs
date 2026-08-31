using System;
using System.Windows;
using System.Windows.Controls;

namespace HeraldHelper.Desktop;

public partial class MainWindow : Window
{
    internal void PickTargetColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TargetColorText, "#FFFFFF");
    }

    internal void PickTimerColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TimerColorText, "#FFFFFF");
    }

    internal void PickOutlineColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(OutlineColorText, "#000000");
    }

    internal void PickTargetOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickOverlayPosition(isTimerOverlay: false);
    }

    internal void PickTimerOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickOverlayPosition(isTimerOverlay: true);
    }

    internal void PickCastOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickCastOverlayPosition();
    }

    internal void PickTargetSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayFontSizeText, "Target", isTimerOverlay: false);
    }

    internal void PickTimerSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayTimerSizeText, "Timers", isTimerOverlay: true);
    }
}
