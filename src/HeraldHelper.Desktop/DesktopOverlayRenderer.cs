using System.Windows;
using System.Windows.Media;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public sealed class DesktopOverlayRenderer : IOverlayRenderer, IDisposable
{
    private readonly Func<IReadOnlyDictionary<string, string>> _getSettings;
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

    public DesktopOverlayRenderer(Func<IReadOnlyDictionary<string, string>> getSettings)
    {
        _getSettings = getSettings;
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

            var settings = _getSettings();
            var showTarget = ReadBool(settings, "overlayShowTarget", true);
            var showTimers = ReadBool(settings, "overlayShowTimers", true);
            var showCastBar = ReadBool(settings, "overlayShowCastBar", true);

            var targetText = showTarget ? BuildTargetText(snapshot.Target, settings) : string.Empty;
            var timerText = showTimers ? BuildTimerText(snapshot.Timers) : string.Empty;

            var ox = ReadInt(settings, "overlayX", 1200);
            var oy = ReadInt(settings, "overlayY", 900);
            var tx = ReadInt(settings, "overlayXTimer", ox + 380);
            var ty = ReadInt(settings, "overlayYTimer", oy);
            var cx = ReadInt(settings, "overlayXCast", ox);
            var cy = ReadInt(settings, "overlayYCast", oy + 86);
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

            var f1 = ReadInt(settings, "fontSize", 20);
            var f2 = ReadInt(settings, "timerSize", f1);
            if (_previewTargetFontSize is not null)
            {
                f1 = _previewTargetFontSize.Value;
            }

            if (_previewTimerFontSize is not null)
            {
                f2 = _previewTimerFontSize.Value;
            }

            var targetColor = ReadColor(settings, "targetColor", Colors.White);
            var timerColor = ReadColor(settings, "timerColor", Colors.White);
            var outlineColor = ReadColor(settings, "outlineColor", Colors.Black);
            if (_previewTargetColor is not null)
            {
                targetColor = _previewTargetColor.Value;
            }

            if (_previewTimerColor is not null)
            {
                timerColor = _previewTimerColor.Value;
            }

            if (_previewOutlineColor is not null)
            {
                outlineColor = _previewOutlineColor.Value;
            }

            _targetWindow.Update(targetText, ox, oy, f1, targetColor, outlineColor);
            _timerWindow.Update(timerText, tx, ty, f2, timerColor, outlineColor);
            var cast = showCastBar ? snapshot.ActiveCast : null;
            _castBarWindow.Update(cast, cx, cy, targetColor, timerColor, outlineColor);
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

    private static bool ReadBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => fallback
        };
    }

    internal static string BuildTargetText(TargetProfile? target, IReadOnlyDictionary<string, string> settings)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Name) || !IsRealPlayerTarget(target))
        {
            return string.Empty;
        }

        var show = settings.TryGetValue("show", out var bits) ? bits : "111110011";
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

    private static int ReadInt(IReadOnlyDictionary<string, string> settings, string key, int fallback)
    {
        return settings.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static MediaColor ReadColor(IReadOnlyDictionary<string, string> settings, string key, MediaColor fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
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
