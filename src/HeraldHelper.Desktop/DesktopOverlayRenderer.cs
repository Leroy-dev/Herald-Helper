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
    private readonly CastBarWindow _castBarWindow;
    private int? _previewTargetX;
    private int? _previewTargetY;
    private int? _previewTimerX;
    private int? _previewTimerY;
    private int? _previewCastX;
    private int? _previewCastY;
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

            var targetText = overlay.ShowTarget ? BuildTargetText(snapshot.Target, _getMap) : string.Empty;
            var timerText = overlay.ShowTimers ? BuildTimerText(snapshot.Timers) : string.Empty;

            _targetWindow.Update(targetText, ox, oy, f1, overlay.TargetFontFamily, targetColor, outlineColor, FontWeights.SemiBold);
            _timerWindow.Update(timerText, tx, ty, f2, overlay.TimerFontFamily, timerColor, outlineColor);
            var cast = overlay.ShowCastBar ? snapshot.ActiveCast : null;
            _castBarWindow.Update(cast, cx, cy, castbarColor, timerColor, outlineColor, overlay.CastbarFontFamily);
        }).Task;
    }

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

    internal static string BuildTargetText(TargetProfile? target, IReadOnlyDictionary<string, string>? map)
    {
        return BuildTargetText(target, map is null ? null : () => map);
    }

    internal static string BuildTargetText(TargetProfile? target, Func<IReadOnlyDictionary<string, string>>? getMap)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Name) || !IsRealPlayerTarget(target))
        {
            return string.Empty;
        }

        var map = getMap?.Invoke();
        var show = map is not null && map.TryGetValue("show", out var bits) ? bits : "111110011";
        bool Flag(int index, bool fallback = true) => show.Length > index ? show[index] == '1' : fallback;

        var line1 = target.Name;
        if (Flag(0) && !string.IsNullOrWhiteSpace(target.Guild))
        {
            line1 += $"  <{target.Guild}>";
        }

        var line2Parts = new List<string>();
        if (Flag(1) && !string.IsNullOrWhiteSpace(target.Class))
        {
            line2Parts.Add(target.Class);
        }
        if (Flag(2) && target.Level is not null)
        {
            line2Parts.Add(target.Level.Value.ToString());
        }
        if (Flag(3) && !string.IsNullOrWhiteSpace(target.RealmRank))
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
        else
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
        if (Flag(4) && target.SoloKills is not null)
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
        _castBarWindow.Close();
    }
}
