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

    internal void PickResistsOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickResistsOverlayPosition();
    }

    internal void PickTargetSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayFontSizeText, "Target", size => _liveOverlay?.SetPreviewFontSize(false, size));
    }

    internal void PickTimerSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayTimerSizeText, "Timers", size => _liveOverlay?.SetPreviewFontSize(true, size));
    }

    internal void PickResistsSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayResistsSizeText, "Resists", size => _liveOverlay?.SetPreviewResistsFontSize(size));
    }
    internal void ReloadOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        ReloadOverlaySettingsFromStore();
        OutputBox.Text = "Overlay settings reloaded.";
    }

    internal void SaveOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new OverlaySettings
        {
            X = NormalizeIntText(OverlayXText.Text, 1200),
            Y = NormalizeIntText(OverlayYText.Text, 900),
            TimerX = NormalizeIntText(OverlayTimerXText.Text, 1580),
            TimerY = NormalizeIntText(OverlayTimerYText.Text, 900),
            CastX = NormalizeIntText(OverlayCastXText.Text, 1200),
            CastY = NormalizeIntText(OverlayCastYText.Text, 986),
            ResistsX = NormalizeIntText(OverlayResistsXText.Text, 1200),
            ResistsY = NormalizeIntText(OverlayResistsYText.Text, 830),
            FontSize = NormalizeIntText(OverlayFontSizeText.Text, 20),
            TimerSize = NormalizeIntText(OverlayTimerSizeText.Text, 20),
            ResistsSize = NormalizeIntText(OverlayResistsSizeText.Text, 20),
            TargetColor = NormalizeColorText(TargetColorText.Text, "#FFFFFF"),
            TimerColor = NormalizeColorText(TimerColorText.Text, "#FFFFFF"),
            OutlineColor = NormalizeColorText(OutlineColorText.Text, "#000000"),
            UseRealmColors = UseRealmColorsCheckbox?.IsChecked ?? true,
            ShowTarget = ShowTargetCheckbox?.IsChecked ?? true,
            ShowTimers = ShowTimersCheckbox?.IsChecked ?? true,
            ShowCastBar = ShowCastBarCheckbox?.IsChecked ?? true,
            ShowResists = ShowResistsCheckbox?.IsChecked ?? false,
            ShowGuild = ShowGuildCheckbox?.IsChecked ?? true,
            ShowClass = ShowClassCheckbox?.IsChecked ?? true,
            ShowLevel = ShowLevelCheckbox?.IsChecked ?? true,
            ShowRealmRank = ShowRealmRankCheckbox?.IsChecked ?? true,
            ShowSoloKills = ShowSoloKillsCheckbox?.IsChecked ?? true,
            DynamicCastSpeedEnabled = DynamicCastSpeedCheckbox?.IsChecked ?? false,
            EstimatedSpellDamageEnabled = EstimatedSpellDamageCheckbox?.IsChecked ?? false,
            OcrReplayEnabled = OcrReplayCheckbox?.IsChecked ?? false,
            TargetFontFamily = OverlayTargetFontCombo?.SelectedItem as string ?? "Segoe UI",
            TimerFontFamily = OverlayTimerFontCombo?.SelectedItem as string ?? "Segoe UI",
            CastbarFontFamily = OverlayCastbarFontCombo?.SelectedItem as string ?? "Segoe UI"
        };

        _overlaySettingsController.Save(settings);
        SaveCurrentCharacterStatBonuses();
        ReloadEditorData();
        _liveOverlay?.ClearPreview();
        ReloadOverlaySettingsFromStore();
        RenderLiveOverlayPreview();
        OutputBox.Text = "Overlay settings saved.";
    }

}
