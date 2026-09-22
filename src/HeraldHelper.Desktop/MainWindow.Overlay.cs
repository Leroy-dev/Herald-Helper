using System;
using System.Windows;
using System.Windows.Controls;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

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

    internal void PickGroupOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickGroupOverlayPosition();
    }

    internal void PickSelfCcOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickSelfCcOverlayPosition();
    }

    internal void PickPeelOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickPeelOverlayPosition();
    }

    internal void PickBuffOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickBuffOverlayPosition();
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

    internal void PreviewOverlay_Click(object sender, RoutedEventArgs e)
    {
        // Fabricated snapshot so the overlay windows (and every Drag/Resize/
        // color picker that re-renders the preview) work without the game.
        // Cleared as soon as a real tick produces a snapshot.
        _demoSnapshot = BuildDemoSnapshot();
        RenderLiveOverlayPreview();
    }

    private static OverlaySnapshot BuildDemoSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        return new OverlaySnapshot(
            new TargetProfile("Preview Player", "Example Guild", "Minstrel", 50, "RR5L2", 34),
            [
                new CcTimerEntry("Preview Player", ControlEffectType.Mezz, now.AddSeconds(48), TargetClass: "Minstrel"),
                new CcTimerEntry("Enemy Tank", ControlEffectType.Stun, now.AddSeconds(9), TargetClass: "Armsman"),
                new CcTimerEntry("Preview Player", ControlEffectType.Root, now.AddSeconds(21), TargetClass: "Minstrel")
            ],
            new CastBarState("Greater Heal", 2.4, now, now.AddSeconds(1.6), null),
            "[Guild] Leroy: inc emain bridge\n[Region] Guard Voldar reports sightings near the tower\nXmlbeastie hits you for 142 damage.",
            new SelfCcState(ControlEffectType.Stun, now.AddSeconds(-4.2)),
            [
                new PeelEntry("Xmlbeastie", 3, now.AddSeconds(-2)),
                new PeelEntry("Moolish", 1, now.AddSeconds(-6))
            ],
            [
                new CooldownEntry("Purge", now.AddSeconds(-7 * 60), now.AddSeconds(13 * 60)),
                new CooldownEntry("Cacophony", now.AddSeconds(-4), now.AddSeconds(11)),
                new CooldownEntry("Ameliorating Melodies", now.AddSeconds(-95))
            ],
            new ClientStateSnapshot(
                [
                    new GroupMemberState(0, "Leroy", "Cleric", 100, 87, 55, 50, "Emain Macha", null, null, null, []),
                    new GroupMemberState(1, "Bowslap", "Hunter", 64, 30, 71, 50, "Emain Macha", null, null, null, []),
                    new GroupMemberState(2, "Tankguy", "Armsman", 22, 8, 96, 50, "Hadrian's Wall", null, null, null, [])
                ],
                [
                    "Toughness III", "Endurance II", "Regrowth", "Spec af",
                    "Damnation", "Serenity", "Acuity III"
                ],
                new PetState("Greater forest wolf", 82, [0], [0], []),
                new SiegeState(42, true, 7, null),
                true, 90, 7963728, 1548245, 75, null, 90));
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
            GroupX = NormalizeIntText(OverlayGroupXText.Text, 40),
            GroupY = NormalizeIntText(OverlayGroupYText.Text, 300),
            GroupSize = NormalizeIntText(OverlayGroupSizeText.Text, 14),
            SelfCcX = NormalizeIntText(OverlaySelfCcXText.Text, 1200),
            SelfCcY = NormalizeIntText(OverlaySelfCcYText.Text, 740),
            SelfCcSize = NormalizeIntText(OverlaySelfCcSizeText.Text, 32),
            PeelX = NormalizeIntText(OverlayPeelXText.Text, 1580),
            PeelY = NormalizeIntText(OverlayPeelYText.Text, 700),
            PeelSize = NormalizeIntText(OverlayPeelSizeText.Text, 16),
            BuffX = NormalizeIntText(OverlayBuffXText.Text, 40),
            BuffY = NormalizeIntText(OverlayBuffYText.Text, 700),
            BuffSize = NormalizeIntText(OverlayBuffSizeText.Text, 14),
            OverlayOpacity = NormalizeIntText(OverlayOpacityText.Text, 100) / 100.0,
            SoundsEnabled = SoundsEnabledCheckbox?.IsChecked ?? false,
            SoundSelfCc = SoundSelfCcCheckbox?.IsChecked ?? true,
            SoundPeel = SoundPeelCheckbox?.IsChecked ?? true,
            SoundInterrupt = SoundInterruptCheckbox?.IsChecked ?? true,
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
            ShowGroup = ShowGroupCheckbox?.IsChecked ?? false,
            ShowSelfCc = ShowSelfCcCheckbox?.IsChecked ?? true,
            ShowPeel = ShowPeelCheckbox?.IsChecked ?? true,
            ShowBuffs = ShowBuffsCheckbox?.IsChecked ?? true,
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
