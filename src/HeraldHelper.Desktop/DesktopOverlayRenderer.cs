using System.Windows;
using System.Windows.Media;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public sealed class DesktopOverlayRenderer : IOverlayRenderer, IDisposable
{
    private readonly Func<OverlaySettings> _getOverlay;
    private readonly Func<IReadOnlyDictionary<string, string>>? _getMap;
    private readonly OverlayTextWindow _targetWindow;
    private readonly OverlayTextWindow _timerWindow;
    private readonly OverlayTextWindow _resistsWindow;
    private readonly CastBarWindow _castBarWindow;
    private int? _previewTargetX;
    private int? _previewTargetY;
    private int? _previewTimerX;
    private int? _previewTimerY;
    private int? _previewCastX;
    private int? _previewCastY;
    private int? _previewResistsX;
    private int? _previewResistsY;
    private MediaColor? _previewTargetColor;
    private MediaColor? _previewTimerColor;
    private MediaColor? _previewOutlineColor;
    private int? _previewTargetFontSize;
    private int? _previewTimerFontSize;
    private bool _disposed;

    public DesktopOverlayRenderer(Func<OverlaySettings> getOverlay, Func<IReadOnlyDictionary<string, string>>? getMap = null)
    {
        _getOverlay = getOverlay;
        _getMap = getMap;
        _targetWindow = new OverlayTextWindow();
        _timerWindow = new OverlayTextWindow();
        _resistsWindow = new OverlayTextWindow();
        _castBarWindow = new CastBarWindow();
    }

    public Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            var overlay = _getOverlay();

            var ox = overlay.X;
            var oy = overlay.Y;
            var tx = overlay.TimerX;
            var ty = overlay.TimerY;
            var cx = overlay.CastX;
            var cy = overlay.CastY;

            if (_previewTargetX is not null)
            {
                ox = _previewTargetX.Value;
            }

            if (_previewTargetY is not null)
            {
                oy = _previewTargetY.Value;
            }

            if (_previewTimerX is not null)
            {
                tx = _previewTimerX.Value;
            }

            if (_previewTimerY is not null)
            {
                ty = _previewTimerY.Value;
            }

            if (_previewCastX is not null)
            {
                cx = _previewCastX.Value;
            }

            if (_previewCastY is not null)
            {
                cy = _previewCastY.Value;
            }

            var rx = _previewResistsX ?? overlay.ResistsX;
            var ry = _previewResistsY ?? overlay.ResistsY;

            var f1 = overlay.FontSize;
            var f2 = overlay.TimerSize;
            if (_previewTargetFontSize is not null)
            {
                f1 = _previewTargetFontSize.Value;
            }

            if (_previewTimerFontSize is not null)
            {
                f2 = _previewTimerFontSize.Value;
            }

            var baseTargetColor = ReadColor(overlay.TargetColor, Colors.White);
            var timerColor = ReadColor(overlay.TimerColor, Colors.White);
            var outlineColor = ReadColor(overlay.OutlineColor, Colors.Black);

            var targetColor = baseTargetColor;
            if (_previewTargetColor is not null)
            {
                targetColor = _previewTargetColor.Value;
            }
            else if (overlay.UseRealmColors && snapshot.Target?.Class is { } targetClass)
            {
                var realm = ClassRealmResolver.Resolve(targetClass);
                if (realm != Realm.Unknown)
                {
                    targetColor = ClassRealmResolver.ResolveColor(realm);
                }
            }

            var castbarColor = _previewTargetColor ?? baseTargetColor;

            if (_previewTimerColor is not null)
            {
                timerColor = _previewTimerColor.Value;
            }

            if (_previewOutlineColor is not null)
            {
                outlineColor = _previewOutlineColor.Value;
            }

            var targetText = overlay.ShowTarget ? BuildTargetText(snapshot.Target, overlay) : string.Empty;
            var timerText = overlay.ShowTimers ? BuildTimerText(snapshot.Timers) : string.Empty;
            var resistsLines = overlay.ShowResists ? BuildResistsLines(snapshot.Target) : null;

            _targetWindow.Update(targetText, ox, oy, f1, overlay.TargetFontFamily, targetColor, outlineColor, FontWeights.SemiBold);
            _timerWindow.Update(timerText, tx, ty, f2, overlay.TimerFontFamily, timerColor, outlineColor);
            _resistsWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color)>?)resistsLines ?? Array.Empty<(string Text, MediaColor Color)>(),
                rx, ry,
                overlay.ResistsSize, overlay.TargetFontFamily, outlineColor);
            var cast = overlay.ShowCastBar ? snapshot.ActiveCast : null;
            _castBarWindow.Update(cast, cx, cy, castbarColor, timerColor, outlineColor, overlay.CastbarFontFamily);
            Rendered?.Invoke(this, new OverlayViewState(
                targetText,
                timerText,
                resistsLines,
                cast,
                targetColor,
                timerColor,
                outlineColor,
                overlay.TargetFontFamily,
                overlay.TimerFontFamily,
                f1,
                f2,
                DateTimeOffset.UtcNow));
        }).Task;
    }

    public event EventHandler<OverlayViewState>? Rendered;

    public void SetPreviewPosition(bool timerOverlay, int x, int y)
    {
        if (timerOverlay)
        {
            _previewTimerX = x;
            _previewTimerY = y;
            return;
        }

        _previewTargetX = x;
        _previewTargetY = y;
    }

    public void SetPreviewColors(MediaColor? targetColor, MediaColor? timerColor, MediaColor? outlineColor)
    {
        _previewTargetColor = targetColor;
        _previewTimerColor = timerColor;
        _previewOutlineColor = outlineColor;
    }

    public void SetPreviewFontSize(bool timerOverlay, int size)
    {
        var normalized = Math.Clamp(size, 10, 72);
        if (timerOverlay)
        {
            _previewTimerFontSize = normalized;
            return;
        }

        _previewTargetFontSize = normalized;
    }

    public void SetPreviewCastBarPosition(int x, int y)
    {
        _previewCastX = x;
        _previewCastY = y;
    }

    public void SetPreviewResistsPosition(int x, int y)
    {
        _previewResistsX = x;
        _previewResistsY = y;
    }

    public void ClearPreview()
    {
        _previewTargetX = null;
        _previewTargetY = null;
        _previewTimerX = null;
        _previewTimerY = null;
        _previewTargetColor = null;
        _previewTimerColor = null;
        _previewOutlineColor = null;
        _previewTargetFontSize = null;
        _previewTimerFontSize = null;
        _previewCastX = null;
        _previewCastY = null;
        _previewResistsX = null;
        _previewResistsY = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            CloseWindows();
            return;
        }

        dispatcher.Invoke(CloseWindows);
    }

    /// <summary>Typed-flag variant — the Overlay view's field checkboxes are the
    /// source of truth; the map overloads remain for tests and legacy cfg.</summary>
    internal static string BuildTargetText(TargetProfile? target, OverlaySettings overlay)
    {
        return BuildTargetTextCore(
            target,
            overlay.ShowGuild,
            overlay.ShowClass,
            overlay.ShowLevel,
            overlay.ShowRealmRank,
            overlay.ShowSoloKills,
            allowDetailFallback: false);
    }

    internal static string BuildTargetText(TargetProfile? target, IReadOnlyDictionary<string, string>? map)
    {
        return BuildTargetText(target, map is null ? null : () => map);
    }

    internal static string BuildTargetText(TargetProfile? target, Func<IReadOnlyDictionary<string, string>>? getMap)
    {
        var map = getMap?.Invoke();
        var show = map is not null && map.TryGetValue("show", out var bits) ? bits : "111110011";
        bool Flag(int index, bool fallback = true) => show.Length > index ? show[index] == '1' : fallback;
        return BuildTargetTextCore(
            target, Flag(0), Flag(1), Flag(2), Flag(3), Flag(4),
            allowDetailFallback: true);
    }

    /// <summary>Thrust/Slash/Crush verdict lines for the resists overlay —
    /// green = target weak to it, red = resists, white = neutral.</summary>
    internal static List<(string Text, MediaColor Color)>? BuildResistsLines(TargetProfile? target)
    {
        if (target is null || !IsRealPlayerTarget(target))
        {
            return null;
        }

        var (thrust, slash, crush) = ClassArmorTable.Lookup(target.Class);
        if (thrust == DamageVerdict.Neutral && slash == DamageVerdict.Neutral && crush == DamageVerdict.Neutral)
        {
            // Cloth/unknown — nothing worth showing.
            return null;
        }

        return
        [
            ("Thrust", VerdictColor(thrust)),
            ("Slash", VerdictColor(slash)),
            ("Crush", VerdictColor(crush)),
        ];
    }

    private static MediaColor VerdictColor(DamageVerdict verdict) =>
        verdict switch
        {
            DamageVerdict.Weak => MediaColor.FromRgb(0x00, 0xFF, 0x00),
            DamageVerdict.Resists => MediaColor.FromRgb(0xFF, 0x09, 0x09),
            _ => MediaColor.FromRgb(0xFF, 0xFF, 0xFF)
        };

    private static string BuildTargetTextCore(
        TargetProfile? target,
        bool showGuild,
        bool showClass,
        bool showLevel,
        bool showRealmRank,
        bool showSoloKills,
        bool allowDetailFallback)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Name) || !IsRealPlayerTarget(target))
        {
            return string.Empty;
        }

        var line1 = target.Name;
        if (showGuild && !string.IsNullOrWhiteSpace(target.Guild))
        {
            line1 += $"  <{target.Guild}>";
        }

        var line2Parts = new List<string>();
        if (showClass && !string.IsNullOrWhiteSpace(target.Class))
        {
            line2Parts.Add(target.Class);
        }
        if (showLevel && target.Level is not null)
        {
            line2Parts.Add(target.Level.Value.ToString());
        }
        if (showRealmRank && !string.IsNullOrWhiteSpace(target.RealmRank))
        {
            line2Parts.Add(target.RealmRank);
        }

        var text = line1;
        if (target.IsLoading)
        {
            text += "\nLoading...";
            return text;
        }

        if (line2Parts.Count > 0)
        {
            text += "\n" + string.Join("  ", line2Parts);
        }
        else if (allowDetailFallback)
        {
            // Fallback for legacy "show" configs that hide details unintentionally.
            var fallbackParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(target.Class))
            {
                fallbackParts.Add(target.Class);
            }
            if (target.Level is not null)
            {
                fallbackParts.Add(target.Level.Value.ToString());
            }
            if (!string.IsNullOrWhiteSpace(target.RealmRank))
            {
                fallbackParts.Add(target.RealmRank);
            }

            if (fallbackParts.Count > 0)
            {
                text += "\n" + string.Join("  ", fallbackParts);
            }
        }
        if (showSoloKills && target.SoloKills is not null)
        {
            text += "\n" + target.SoloKills.Value.ToString("N0");
        }

        return text;
    }

    private static string BuildTimerText(IReadOnlyCollection<CcTimerEntry> timers)
    {
        if (timers.Count == 0)
        {
            return string.Empty;
        }

        var now = DateTimeOffset.UtcNow;
        var rows = timers
            .OrderBy(x => x.TargetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.EffectType)
            .Select(x => $"{ShortType(x.EffectType)} {x.TargetName} {x.RemainingSeconds(now)}")
            .Take(12);

        return string.Join("\n", rows);
    }

    private static string ShortType(ControlEffectType type)
    {
        return type switch
        {
            ControlEffectType.Mezz => "M",
            ControlEffectType.Stun => "S",
            ControlEffectType.Root => "R",
            _ => "?"
        };
    }

    private static MediaColor ReadColor(string? raw, MediaColor fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            var converted = System.Windows.Media.ColorConverter.ConvertFromString(raw.Trim());
            return converted is MediaColor color ? color : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    internal static bool IsRealPlayerTarget(TargetProfile target)
    {
        return target.IsLoading
            || !string.IsNullOrWhiteSpace(target.Class)
            || !string.IsNullOrWhiteSpace(target.Guild)
            || target.Level is not null
            || !string.IsNullOrWhiteSpace(target.RealmRank)
            || target.SoloKills is not null;
    }

    private void CloseWindows()
    {
        if (_targetWindow.IsVisible)
        {
            _targetWindow.Hide();
        }

        if (_timerWindow.IsVisible)
        {
            _timerWindow.Hide();
        }

        _targetWindow.Close();
        _timerWindow.Close();
        _resistsWindow.Close();
        _castBarWindow.Close();
    }
}
