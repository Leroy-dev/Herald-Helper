using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using MaterialDesignThemes.Wpf;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public partial class MainWindow : Window
{
    /// <summary>Live overlay settings object — controls mutate it directly and
    /// the renderer reads it every frame. Persistence is a debounced save.</summary>
    private OverlaySettings _overlaySettings = new();
    private DispatcherTimer? _overlaySaveTimer;
    private List<OverlayElementDef>? _overlayElements;
    private IReadOnlyList<string> _overlayFontFamilies = [];

    /// <summary>One row in the Overlay Elements table — every window gets the
    /// same show/position/size/font controls.</summary>
    private sealed class OverlayElementDef
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public string? ToolTip { get; init; }
        public required Func<OverlaySettings, bool> GetShow { get; init; }
        public required Action<OverlaySettings, bool> SetShow { get; init; }
        public required Func<OverlaySettings, int> GetX { get; init; }
        public required Action<OverlaySettings, int> SetX { get; init; }
        public required Func<OverlaySettings, int> GetY { get; init; }
        public required Action<OverlaySettings, int> SetY { get; init; }
        public required Func<OverlaySettings, int> GetSize { get; init; }
        public required Action<OverlaySettings, int> SetSize { get; init; }
        public required Func<OverlaySettings, string> GetFont { get; init; }
        public required Action<OverlaySettings, string> SetFont { get; init; }
        public bool Hidden { get; init; }
        public WpfCheckBox? ShowBox { get; set; }
        public WpfTextBox? XBox { get; set; }
        public WpfTextBox? YBox { get; set; }
        public WpfTextBox? SizeBox { get; set; }
        public WpfComboBox? FontBox { get; set; }
    }

    private List<OverlayElementDef> BuildOverlayElementDefs() =>
    [
        new()
        {
            Key = "target", Label = "Target",
            ToolTip = "Target info block — name, guild, class, level, realm rank, solo kills.",
            GetShow = s => s.ShowTarget, SetShow = (s, v) => s.ShowTarget = v,
            GetX = s => s.X, SetX = (s, v) => s.X = v,
            GetY = s => s.Y, SetY = (s, v) => s.Y = v,
            GetSize = s => s.FontSize, SetSize = (s, v) => s.FontSize = v,
            GetFont = s => s.TargetFontFamily, SetFont = (s, v) => s.TargetFontFamily = v
        },
        new()
        {
            Key = "timers", Label = "Timers",
            ToolTip = "CC immunity timers — target, effect and remaining time.",
            GetShow = s => s.ShowTimers, SetShow = (s, v) => s.ShowTimers = v,
            GetX = s => s.TimerX, SetX = (s, v) => s.TimerX = v,
            GetY = s => s.TimerY, SetY = (s, v) => s.TimerY = v,
            GetSize = s => s.TimerSize, SetSize = (s, v) => s.TimerSize = v,
            GetFont = s => s.TimerFontFamily, SetFont = (s, v) => s.TimerFontFamily = v
        },
        new()
        {
            Key = "cooldowns", Label = "Cooldowns",
            ToolTip = "Spell recasts and realm-ability cooldowns — purple countdown lines, newest first.",
            GetShow = s => s.ShowCooldowns, SetShow = (s, v) => s.ShowCooldowns = v,
            GetX = s => s.CooldownsX, SetX = (s, v) => s.CooldownsX = v,
            GetY = s => s.CooldownsY, SetY = (s, v) => s.CooldownsY = v,
            GetSize = s => s.EffectiveCooldownsSize, SetSize = (s, v) => s.CooldownsSize = v,
            GetFont = s => s.EffectiveCooldownsFontFamily, SetFont = (s, v) => s.CooldownsFontFamily = v
        },
        new()
        {
            Key = "castbar", Label = "Spellbar",
            ToolTip = "Your active cast — spell name, progress bar and remaining seconds.",
            GetShow = s => s.ShowCastBar, SetShow = (s, v) => s.ShowCastBar = v,
            GetX = s => s.CastX, SetX = (s, v) => s.CastX = v,
            GetY = s => s.CastY, SetY = (s, v) => s.CastY = v,
            GetSize = s => s.CastSize, SetSize = (s, v) => s.CastSize = v,
            GetFont = s => s.CastbarFontFamily, SetFont = (s, v) => s.CastbarFontFamily = v
        },
        new()
        {
            Key = "resists", Label = "Resists",
            ToolTip = "Thrust/Slash/Crush verdicts for the target's armor class — green = weak, red = resists.",
            GetShow = s => s.ShowResists, SetShow = (s, v) => s.ShowResists = v,
            GetX = s => s.ResistsX, SetX = (s, v) => s.ResistsX = v,
            GetY = s => s.ResistsY, SetY = (s, v) => s.ResistsY = v,
            GetSize = s => s.ResistsSize, SetSize = (s, v) => s.ResistsSize = v,
            GetFont = s => s.EffectiveResistsFontFamily, SetFont = (s, v) => s.ResistsFontFamily = v
        },
        new()
        {
            Key = "group", Label = "Group",
            Hidden = true, // experimental — needs adapter stats feed
            ToolTip = "Group member frames (name, class, HP/End/Pow bars) — adapter data, needs stats memory read.",
            GetShow = s => s.ShowGroup, SetShow = (s, v) => s.ShowGroup = v,
            GetX = s => s.GroupX, SetX = (s, v) => s.GroupX = v,
            GetY = s => s.GroupY, SetY = (s, v) => s.GroupY = v,
            GetSize = s => s.GroupSize, SetSize = (s, v) => s.GroupSize = v,
            GetFont = s => s.EffectiveGroupFontFamily, SetFont = (s, v) => s.GroupFontFamily = v
        },
        new()
        {
            Key = "selfcc", Label = "Self-CC",
            ToolTip = "Big banner when you get stunned/mesmerized/rooted — shows elapsed time since the CC landed.",
            GetShow = s => s.ShowSelfCc, SetShow = (s, v) => s.ShowSelfCc = v,
            GetX = s => s.SelfCcX, SetX = (s, v) => s.SelfCcX = v,
            GetY = s => s.SelfCcY, SetY = (s, v) => s.SelfCcY = v,
            GetSize = s => s.SelfCcSize, SetSize = (s, v) => s.SelfCcSize = v,
            GetFont = s => s.EffectiveSelfCcFontFamily, SetFont = (s, v) => s.SelfCcFontFamily = v
        },
        new()
        {
            Key = "peel", Label = "Peel",
            ToolTip = "List of enemies who attacked you recently (attacker + hit count) — who's trying to peel you.",
            GetShow = s => s.ShowPeel, SetShow = (s, v) => s.ShowPeel = v,
            GetX = s => s.PeelX, SetX = (s, v) => s.PeelX = v,
            GetY = s => s.PeelY, SetY = (s, v) => s.PeelY = v,
            GetSize = s => s.PeelSize, SetSize = (s, v) => s.PeelSize = v,
            GetFont = s => s.EffectivePeelFontFamily, SetFont = (s, v) => s.PeelFontFamily = v
        },
        new()
        {
            Key = "buffs", Label = "Buffs",
            ToolTip = "Your active buffs with icons — read live from the client's EFFECTS array, needs stats memory read.",
            GetShow = s => s.ShowBuffs, SetShow = (s, v) => s.ShowBuffs = v,
            GetX = s => s.BuffX, SetX = (s, v) => s.BuffX = v,
            GetY = s => s.BuffY, SetY = (s, v) => s.BuffY = v,
            GetSize = s => s.BuffSize, SetSize = (s, v) => s.BuffSize = v,
            GetFont = s => s.EffectiveBuffFontFamily, SetFont = (s, v) => s.BuffFontFamily = v
        },
        new()
        {
            Key = "pet", Label = "Pet",
            ToolTip = "Pet vitals plus its active-effect icons — adapter data, needs stats memory read.",
            GetShow = s => s.ShowPet, SetShow = (s, v) => s.ShowPet = v,
            GetX = s => s.PetX, SetX = (s, v) => s.PetX = v,
            GetY = s => s.PetY, SetY = (s, v) => s.PetY = v,
            GetSize = s => s.EffectivePetSize, SetSize = (s, v) => s.PetSize = v,
            GetFont = s => s.EffectivePetFontFamily, SetFont = (s, v) => s.PetFontFamily = v
        }
    ];

    /// <summary>Generates the uniform element table — show checkbox, label,
    /// X/Y + drag, size + resize, font picker per overlay window.</summary>
    private void BuildOverlayElementsPanel(IReadOnlyList<string> fontFamilies)
    {
        _overlayElements = BuildOverlayElementDefs();
        _overlayFontFamilies = fontFamilies;

        var panel = OverlayView?.OverlayElementsPanel;
        if (panel is null)
        {
            return;
        }

        panel.Children.Clear();
        panel.Children.Add(BuildOverlayElementsHeader());
        foreach (var def in _overlayElements.Where(d => !d.Hidden))
        {
            panel.Children.Add(BuildOverlayElementRow(def));
        }
    }

    private Grid BuildOverlayElementsHeader()
    {
        var grid = OverlayElementRowGrid();
        grid.Margin = new Thickness(0, 0, 0, 2);
        var muted = (System.Windows.Media.Brush)FindResource("VsMutedTextBrush");
        var labels = new[] { "", "Element", "X", "Y", "", "Size", "", "Font" };
        for (var i = 0; i < labels.Length; i++)
        {
            var text = new TextBlock
            {
                Text = labels[i],
                Foreground = muted,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }
        return grid;
    }

    private static Grid OverlayElementRowGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });              // show
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });          // label
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });           // X
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });           // Y
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });              // drag
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });           // size
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });              // resize
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });          // font
        return grid;
    }

    private Grid BuildOverlayElementRow(OverlayElementDef def)
    {
        var grid = OverlayElementRowGrid();
        grid.Margin = new Thickness(0, 3, 0, 3);

        var show = new WpfCheckBox
        {
            IsChecked = def.GetShow(_overlaySettings),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"Show the {def.Label} overlay"
        };
        show.Checked += (_, _) => { def.SetShow(_overlaySettings, true); ScheduleOverlaySave(); };
        show.Unchecked += (_, _) => { def.SetShow(_overlaySettings, false); ScheduleOverlaySave(); };
        Grid.SetColumn(show, 0);
        grid.Children.Add(show);
        def.ShowBox = show;

        var label = new TextBlock
        {
            Text = def.Label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            ToolTip = def.ToolTip
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        def.XBox = BuildElementIntBox("X", 2, grid);
        def.YBox = BuildElementIntBox("Y", 3, grid);

        var drag = BuildElementButton(PackIconKind.CursorMove, "Drag", $"Drag the {def.Label} overlay position");
        drag.Click += (_, _) => PickElementPosition(def);
        Grid.SetColumn(drag, 4);
        grid.Children.Add(drag);

        def.SizeBox = BuildElementIntBox("Size", 5, grid);

        var resize = BuildElementButton(PackIconKind.ArrowExpand, "Size", $"Resize the {def.Label} overlay");
        resize.Click += (_, _) => PickElementSize(def);
        Grid.SetColumn(resize, 6);
        grid.Children.Add(resize);

        var font = new WpfComboBox
        {
            ItemsSource = _overlayFontFamilies,
            SelectedItem = def.GetFont(_overlaySettings),
            Style = (Style)FindResource("CompactInputComboBoxStyle"),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"{def.Label} font family"
        };
        font.SelectionChanged += (_, _) =>
        {
            if (font.SelectedItem is string family)
            {
                def.SetFont(_overlaySettings, family);
                ScheduleOverlaySave();
            }
        };
        Grid.SetColumn(font, 7);
        grid.Children.Add(font);
        def.FontBox = font;

        return grid;
    }

    private WpfTextBox BuildElementIntBox(string hint, int column, Grid grid)
    {
        var box = new WpfTextBox
        {
            Width = 44,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("CompactInputTextBoxStyle")
        };
        MaterialDesignThemes.Wpf.HintAssist.SetHint(box, hint);
        Grid.SetColumn(box, column);
        grid.Children.Add(box);
        return box;
    }

    private WpfButton BuildElementButton(PackIconKind icon, string text, string toolTip)
    {
        var button = new WpfButton
        {
            Style = (Style)FindResource("ToolbarButtonStyle"),
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(6, 3, 6, 3),
            ToolTip = toolTip,
            Content = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Children =
                {
                    new PackIcon
                    {
                        Kind = icon,
                        Width = 14,
                        Height = 14,
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        return button;
    }

    /// <summary>Pushes the live settings into every generated element row —
    /// suppresses change events while controls sync.</summary>
    private void PushOverlayElementValues()
    {
        if (_overlayElements is null)
        {
            return;
        }

        _isBindingControls = true;
        try
        {
            foreach (var def in _overlayElements)
            {
                if (def.ShowBox is not null)
                {
                    def.ShowBox.IsChecked = def.GetShow(_overlaySettings);
                }
                if (def.XBox is not null)
                {
                    def.XBox.Text = def.GetX(_overlaySettings).ToString();
                }
                if (def.YBox is not null)
                {
                    def.YBox.Text = def.GetY(_overlaySettings).ToString();
                }
                if (def.SizeBox is not null)
                {
                    def.SizeBox.Text = def.GetSize(_overlaySettings).ToString();
                }
                if (def.FontBox is not null)
                {
                    SelectFont(def.FontBox, def.GetFont(_overlaySettings));
                }
            }
        }
        finally
        {
            _isBindingControls = false;
        }
    }

    /// <summary>TextChanged hook for the generated X/Y/Size boxes — writes
    /// valid integers straight into the live settings object.</summary>
    private void HookElementIntBox(WpfTextBox? box, Action<OverlaySettings, int> setter)
    {
        if (box is null)
        {
            return;
        }

        box.TextChanged += (_, _) =>
        {
            if (_isBindingControls || !int.TryParse(box.Text.Trim(), out var value))
            {
                return;
            }
            setter(_overlaySettings, value);
            ScheduleOverlaySave();
        };
    }

    /// <summary>Wires every element row's text boxes to the live settings.</summary>
    private void HookOverlayElementEvents()
    {
        if (_overlayElements is null)
        {
            return;
        }

        foreach (var def in _overlayElements)
        {
            HookElementIntBox(def.XBox, def.SetX);
            HookElementIntBox(def.YBox, def.SetY);
            HookElementIntBox(def.SizeBox, def.SetSize);
        }
    }

    /// <summary>Wires the non-element overlay controls (sounds, target fields,
    /// behavior flags, opacity, colors) to the live settings object.</summary>
    private void WireOverlayScalarControls()
    {
        HookOverlayCheck(ShowGuildCheckbox, (s, v) => s.ShowGuild = v);
        HookOverlayCheck(ShowClassCheckbox, (s, v) => s.ShowClass = v);
        HookOverlayCheck(ShowLevelCheckbox, (s, v) => s.ShowLevel = v);
        HookOverlayCheck(ShowRealmRankCheckbox, (s, v) => s.ShowRealmRank = v);
        HookOverlayCheck(ShowSoloKillsCheckbox, (s, v) => s.ShowSoloKills = v);
        HookOverlayCheck(UseRealmColorsCheckbox, (s, v) => s.UseRealmColors = v);
        HookOverlayCheck(SoundsEnabledCheckbox, (s, v) => s.SoundsEnabled = v);
        HookOverlayCheck(SoundSelfCcCheckbox, (s, v) => s.SoundSelfCc = v);
        HookOverlayCheck(SoundPeelCheckbox, (s, v) => s.SoundPeel = v);
        HookOverlayCheck(SoundInterruptCheckbox, (s, v) => s.SoundInterrupt = v);
        HookOverlayCheck(DynamicCastSpeedCheckbox, (s, v) => s.DynamicCastSpeedEnabled = v);
        HookOverlayCheck(EstimatedSpellDamageCheckbox, (s, v) => s.EstimatedSpellDamageEnabled = v);
        HookOverlayCheck(OcrReplayCheckbox, (s, v) => s.OcrReplayEnabled = v);

        OverlayOpacityText.TextChanged += (_, _) =>
        {
            if (_isBindingControls || !int.TryParse(OverlayOpacityText.Text.Trim(), out var percent))
            {
                return;
            }
            _overlaySettings.OverlayOpacity = Math.Clamp(percent, 30, 100) / 100.0;
            ScheduleOverlaySave();
        };

        TargetColorText.LostFocus += (_, _) => ApplyColorBox(TargetColorText, "#FFFFFF", (s, v) => s.TargetColor = v);
        TimerColorText.LostFocus += (_, _) => ApplyColorBox(TimerColorText, "#FFFFFF", (s, v) => s.TimerColor = v);
        OutlineColorText.LostFocus += (_, _) => ApplyColorBox(OutlineColorText, "#000000", (s, v) => s.OutlineColor = v);
        TargetColorText.TextChanged += (_, _) => OverlayColorTextChanged();
        TimerColorText.TextChanged += (_, _) => OverlayColorTextChanged();
        OutlineColorText.TextChanged += (_, _) => OverlayColorTextChanged();

        CastingSpeedBonusText.LostFocus += (_, _) => ScheduleOverlaySave();
        SpellDamageBonusText.LostFocus += (_, _) => ScheduleOverlaySave();
    }

    private void HookOverlayCheck(WpfCheckBox? box, Action<OverlaySettings, bool> setter)
    {
        if (box is null)
        {
            return;
        }

        RoutedEventHandler handler = (_, _) =>
        {
            if (_isBindingControls)
            {
                return;
            }
            setter(_overlaySettings, box.IsChecked == true);
            ScheduleOverlaySave();
        };
        box.Checked += handler;
        box.Unchecked += handler;
    }

    private void ApplyColorBox(
        WpfTextBox box,
        string fallback,
        Action<OverlaySettings, string> setter)
    {
        if (_isBindingControls)
        {
            return;
        }

        var normalized = NormalizeColorText(box.Text, fallback);
        box.Text = normalized;
        setter(_overlaySettings, normalized);
        _liveOverlay?.ClearPreview();
        ApplyOverlayColorPreviewFromInputs();
        ScheduleOverlaySave();
    }

    /// <summary>Typing a hex color previews it live; the normalized value is
    /// written back on focus loss.</summary>
    private void OverlayColorTextChanged()
    {
        if (_isBindingControls)
        {
            return;
        }
        ApplyOverlayColorPreviewFromInputs();
        RenderLiveOverlayPreview();
    }

    /// <summary>Debounce — rapid changes (typing, drag) collapse into one
    /// save+rebuild so the runtime and preview always follow the UI.</summary>
    private void ScheduleOverlaySave()
    {
        if (_isBindingControls)
        {
            return;
        }

        if (_overlaySaveTimer is null)
        {
            _overlaySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _overlaySaveTimer.Tick += (_, _) => PersistOverlaySettings();
        }

        _overlaySaveTimer.Stop();
        _overlaySaveTimer.Start();
    }

    /// <summary>Persist immediately — used by drag/resize/color pickers once
    /// the user commits a value.</summary>
    private void PersistOverlaySettings()
    {
        _overlaySaveTimer?.Stop();
        _overlaySettingsController.Save(_overlaySettings);
        SaveCurrentCharacterStatBonuses();
        RebuildRuntimeFromFiles();
        RefreshSetupChecklist();
        _liveOverlay?.ClearPreview();
        RenderLiveOverlayPreview();
    }

    internal void PickTargetColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TargetColorText, "#FFFFFF", (s, v) => s.TargetColor = v);
    }

    internal void PickTimerColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TimerColorText, "#FFFFFF", (s, v) => s.TimerColor = v);
    }

    internal void PickOutlineColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(OutlineColorText, "#000000", (s, v) => s.OutlineColor = v);
    }

    /// <summary>Live drag: updates settings + preview as the cursor moves;
    /// commit persists, cancel restores the previous position.</summary>
    private void PickElementPosition(OverlayElementDef def)
    {
        var originalX = def.GetX(_overlaySettings);
        var originalY = def.GetY(_overlaySettings);

        var selected = OverlayCursorPickerWindow.Pick(this, (x, y) =>
        {
            def.SetX(_overlaySettings, x);
            def.SetY(_overlaySettings, y);
            PushOverlayElementValues();
            _liveOverlay?.SetPreviewPosition(def.Key, x, y);
            RenderLiveOverlayPreview();
        });

        _liveOverlay?.ClearPreview();
        if (selected is null)
        {
            def.SetX(_overlaySettings, originalX);
            def.SetY(_overlaySettings, originalY);
            PushOverlayElementValues();
            RenderLiveOverlayPreview();
            return;
        }

        def.SetX(_overlaySettings, selected.Value.X);
        def.SetY(_overlaySettings, selected.Value.Y);
        PushOverlayElementValues();
        PersistOverlaySettings();
    }

    /// <summary>Live resize: same pattern as position drag — the size slider
    /// previews through the overlay renderer until accepted or cancelled.</summary>
    private void PickElementSize(OverlayElementDef def)
    {
        var original = def.GetSize(_overlaySettings);

        var selected = LiveSizePickerWindow.Pick(this, def.Label, original, size =>
        {
            def.SetSize(_overlaySettings, size);
            PushOverlayElementValues();
            _liveOverlay?.SetPreviewFontSize(def.Key, size);
            RenderLiveOverlayPreview();
        });

        _liveOverlay?.ClearPreview();
        if (selected is null)
        {
            def.SetSize(_overlaySettings, original);
            PushOverlayElementValues();
            RenderLiveOverlayPreview();
            return;
        }

        def.SetSize(_overlaySettings, selected.Value);
        PushOverlayElementValues();
        PersistOverlaySettings();
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
                [
                    new SelfEffect("Buffbot Spec Str Con", 66),
                    new SelfEffect("Prophets's Barrier", 13)
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
}
