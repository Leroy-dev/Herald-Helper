using System;
using System.Windows;
using System.Windows.Controls;
using HeraldHelper.Desktop.Models;

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
    internal void ReloadOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        ReloadOverlaySettingsFromStore();
        OutputBox.Text = "Overlay settings reloaded.";
    }

    internal void SaveOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new OverlaySettings(
            NormalizeIntText(OverlayXText.Text, 1200),
            NormalizeIntText(OverlayYText.Text, 900),
            NormalizeIntText(OverlayTimerXText.Text, 1580),
            NormalizeIntText(OverlayTimerYText.Text, 900),
            NormalizeIntText(OverlayCastXText.Text, 1200),
            NormalizeIntText(OverlayCastYText.Text, 986),
            NormalizeIntText(OverlayFontSizeText.Text, 20),
            NormalizeIntText(OverlayTimerSizeText.Text, 20),
            NormalizeColorText(TargetColorText.Text, "#FFFFFF"),
            NormalizeColorText(TimerColorText.Text, "#FFFFFF"),
            NormalizeColorText(OutlineColorText.Text, "#000000"),
            ShowTargetCheckbox?.IsChecked ?? true,
            ShowTimersCheckbox?.IsChecked ?? true,
            ShowCastBarCheckbox?.IsChecked ?? true,
            DynamicCastSpeedCheckbox?.IsChecked ?? false,
            EstimatedSpellDamageCheckbox?.IsChecked ?? false,
            OcrReplayCheckbox?.IsChecked ?? false);

        _settingsController.Save(_overlaySettingsController.Save(settings));
        SaveCurrentCharacterStatBonuses();
        ReloadEditorData();
        _liveOverlay?.ClearPreview();
        ReloadOverlaySettingsFromStore();
        RenderLiveOverlayPreview();
        OutputBox.Text = "Overlay settings saved.";
    }

}
