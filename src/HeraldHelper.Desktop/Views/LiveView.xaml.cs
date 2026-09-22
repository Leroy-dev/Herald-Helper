using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Views;

public partial class LiveView : System.Windows.Controls.UserControl
{
    private readonly IconImageLoader _iconLoader = new();

    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public LiveView()
    {
        InitializeComponent();
    }

    private void OpenReplay_Click(object sender, RoutedEventArgs e)
    {
        new OcrReplayWindow { Owner = Main }.Show();
    }

    private void ClearResponseDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Main?.ClearResponseDiagnostics_Click(sender, e);
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        // One bundle for bug reports: status, OCR diagnostics, response log.
        var bundle =
            "=== Status ===\n" + OutputBox.Text +
            "\n\n=== OCR Diagnostics ===\n" + DiagnosticsBox.Text +
            "\n\n=== Response Diagnostics ===\n" + ResponseDiagnosticsBox.Text;
        System.Windows.Clipboard.SetText(bundle);
    }

    /// <summary>
    /// Mirrors the last rendered overlay state into the Live panel — same
    /// text, fonts and colors the on-screen overlay windows just drew.
    /// </summary>
    public void UpdateMirror(OverlayViewState state)
    {
        var hasContent = !string.IsNullOrWhiteSpace(state.TargetText) ||
                         !string.IsNullOrWhiteSpace(state.TimerText) ||
                         state.ResistsLines is { Count: > 0 } ||
                         state.Cast is not null;
        MirrorEmptyText.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;

        if (state.ResistsLines is { Count: > 0 } resistsLines)
        {
            MirrorResistsText.Inlines.Clear();
            for (var i = 0; i < resistsLines.Count; i++)
            {
                if (i > 0)
                {
                    MirrorResistsText.Inlines.Add(new Run("  "));
                }
                AppendOverlayLine(MirrorResistsText, resistsLines[i], Math.Clamp(state.ResistsFontSize, 9, 20));
            }
            MirrorResistsText.Visibility = Visibility.Visible;
        }
        else
        {
            MirrorResistsText.Visibility = Visibility.Collapsed;
        }

        MirrorTargetText.Text = state.TargetText;
        MirrorTargetText.Foreground = new SolidColorBrush(state.TargetColor);
        MirrorTargetText.FontFamily = new System.Windows.Media.FontFamily(state.TargetFontFamily);
        MirrorTargetText.FontSize = Math.Clamp(state.TargetFontSize, 11, 22);

        if (state.TimerLines is { Count: > 0 } timerLines)
        {
            MirrorTimerText.Inlines.Clear();
            for (var i = 0; i < timerLines.Count; i++)
            {
                if (i > 0)
                {
                    MirrorTimerText.Inlines.Add(new LineBreak());
                }
                AppendOverlayLine(MirrorTimerText, timerLines[i], Math.Clamp(state.TimerFontSize, 10, 18));
            }
        }
        else
        {
            MirrorTimerText.Text = state.TimerText;
            MirrorTimerText.Foreground = new SolidColorBrush(state.TimerColor);
        }
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

    private void AppendOverlayLine(
        TextBlock target,
        (string Text, System.Windows.Media.Color Color, IconSpriteRef? Icon) line,
        double fontSize)
    {
        if (line.Icon is { } iconRef)
        {
            var image = _iconLoader.Load(iconRef);
            if (image is not null)
            {
                target.Inlines.Add(new InlineUIContainer(
                    new System.Windows.Controls.Image
                    {
                        Source = image,
                        Width = fontSize,
                        Height = fontSize,
                        Margin = new Thickness(0, 0, 3, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    }));
            }
        }

        target.Inlines.Add(new Run(line.Text) { Foreground = new SolidColorBrush(line.Color) });
    }

    /// <summary>
    /// Renders the client's own live adapter values (read from process memory)
    /// below the mirror — the player's vitals/resists, not the target's.
    /// </summary>
    public void UpdateClientState(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            PlayerStatePanel.Visibility = Visibility.Collapsed;
            return;
        }
        PlayerStatePanel.Visibility = Visibility.Visible;
        PlayerBindText.Text = $"{values.Count} adapters";

        var name = V(values, "stats_name");
        var cls = V(values, "stats_profession") ?? V(values, "stats_base_class");
        var race = V(values, "stats_race");
        var level = V(values, "stats_level");
        var rp = V(values, "stats_realm_points");
        PlayerIdentityText.Text = string.Join("  ·  ", new[]
            {
                name,
                string.Join(" ", new[] { race, cls }.Where(x => x is not null)),
                level is null ? null : $"Level {level}",
                rp is null ? null : $"RP {rp}",
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var target = V(values, "summary_target");
        if (target is null)
        {
            PlayerTargetText.Text = "Target: —";
            PlayerTargetText.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            var hits = V(values, "summary_target_hits");
            PlayerTargetText.Text = $"Target: {target}{(hits is null ? string.Empty : $"  ·  {hits}%")}";
            var conColor = V(values, "summary_target_color") is { } raw ? ParseHexColor(raw) : null;
            PlayerTargetText.Foreground = conColor is { } c
                ? new SolidColorBrush(c)
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x8A, 0x8A));
        }

        // stats_hitpoints is the character-sheet MAX — current health is the
        // summary bar's percent (summary_player_hits), so show cur/max.
        var hitsPct = N(values, "summary_player_hits");
        var maxHp = N(values, "stats_hitpoints");
        var hpText = hitsPct is { } pct && maxHp is { } max && max > 0
            ? $"{(int)Math.Round(pct * max / 100)}/{(int)max}"
            : hitsPct is { } p ? $"{p:0}%" : "—";
        PlayerVitalsText.Text = $"HP {hpText}    End {V(values, "summary_player_end") ?? "—"}    Pow {V(values, "summary_player_power") ?? "—"}    Conc {V(values, "concentration") ?? "—"}";

        PlayerWeaponText.Text = $"Dmg {V(values, "stats_weapon_damage") ?? "—"}    Skill {V(values, "stats_weapon_skill") ?? "—"}    AF {V(values, "stats_armor_factor") ?? "—"}    BP {V(values, "bounty_points") ?? "—"}";

        var resists = new[]
        {
            ("Thrust", V(values, "stats_thrust")), ("Crush", V(values, "stats_crush")),
            ("Slash", V(values, "stats_slash")), ("Heat", V(values, "stats_heat")),
            ("Cold", V(values, "stats_cold")), ("Matter", V(values, "stats_matter")),
            ("Energy", V(values, "stats_energy")), ("Spirit", V(values, "stats_spirit")),
            ("Body", V(values, "stats_body")),
        };
        PlayerResistsText.Text = resists.Any(r => r.Item2 is not null)
            ? string.Join("   ", resists.Select(r => $"{r.Item1} {r.Item2 ?? "—"}"))
            : "Resists: —";
    }

    private static string? V(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static double? N(IReadOnlyDictionary<string, string> values, string key) =>
        V(values, key) is { } raw &&
        double.TryParse(raw.Trim().TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    /// <summary>Client con colors arrive as RRGGBB (no '#').</summary>
    private static System.Windows.Media.Color? ParseHexColor(string raw)
    {
        var t = raw.Trim().TrimStart('#');
        return t.Length == 6 && int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var rgb)
            ? System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : null;
    }
}
