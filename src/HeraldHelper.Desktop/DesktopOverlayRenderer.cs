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
    private readonly GroupOverlayWindow _groupWindow;
    private readonly OverlayTextWindow _selfCcWindow;
    private readonly OverlayTextWindow _peelWindow;
    private readonly OverlayTextWindow _worldWindow;
    private readonly OverlayTextWindow _buffWindow;
    private readonly OverlayTextWindow _chatWindow;
    private int? _previewTargetX;
    private int? _previewTargetY;
    private int? _previewTimerX;
    private int? _previewTimerY;
    private int? _previewCastX;
    private int? _previewCastY;
    private int? _previewResistsX;
    private int? _previewResistsY;
    private int? _previewResistsFontSize;
    private int? _previewGroupX;
    private int? _previewGroupY;
    private int? _previewSelfCcX;
    private int? _previewSelfCcY;
    private int? _previewPeelX;
    private int? _previewPeelY;
    private int? _previewWorldX;
    private int? _previewWorldY;
    private int? _previewBuffX;
    private int? _previewBuffY;
    private int? _previewChatX;
    private int? _previewChatY;
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
        _groupWindow = new GroupOverlayWindow();
        _selfCcWindow = new OverlayTextWindow();
        _peelWindow = new OverlayTextWindow();
        _worldWindow = new OverlayTextWindow();
        _buffWindow = new OverlayTextWindow();
        _chatWindow = new OverlayTextWindow();
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

            var opacity = Math.Clamp(overlay.OverlayOpacity, 0.3, 1.0);
            _targetWindow.Opacity = opacity;
            _timerWindow.Opacity = opacity;
            _resistsWindow.Opacity = opacity;
            _castBarWindow.Opacity = opacity;
            _groupWindow.Opacity = opacity;
            _selfCcWindow.Opacity = opacity;
            _peelWindow.Opacity = opacity;
            _worldWindow.Opacity = opacity;
            _buffWindow.Opacity = opacity;
            _chatWindow.Opacity = opacity;

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
            var gx = _previewGroupX ?? overlay.GroupX;
            var gy = _previewGroupY ?? overlay.GroupY;
            var scx = _previewSelfCcX ?? overlay.SelfCcX;
            var scy = _previewSelfCcY ?? overlay.SelfCcY;
            var px = _previewPeelX ?? overlay.PeelX;
            var py = _previewPeelY ?? overlay.PeelY;
            var wx = _previewWorldX ?? overlay.WorldX;
            var wy = _previewWorldY ?? overlay.WorldY;
            var bx = _previewBuffX ?? overlay.BuffX;
            var by = _previewBuffY ?? overlay.BuffY;
            var chx = _previewChatX ?? overlay.ChatX;
            var chy = _previewChatY ?? overlay.ChatY;

            var f1 = overlay.FontSize;
            var f2 = overlay.TimerSize;
            var f3 = overlay.ResistsSize;
            if (_previewTargetFontSize is not null)
            {
                f1 = _previewTargetFontSize.Value;
            }

            if (_previewTimerFontSize is not null)
            {
                f2 = _previewTimerFontSize.Value;
            }

            if (_previewResistsFontSize is not null)
            {
                f3 = _previewResistsFontSize.Value;
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
            var timerLines = overlay.ShowTimers ? BuildTimerLines(snapshot.Timers, timerColor) : null;
            var timerText = timerLines is null ? string.Empty : string.Join("\n", timerLines.Select(l => l.Text));
            var resistsLines = overlay.ShowResists ? BuildResistsLines(snapshot.Target) : null;

            _targetWindow.Update(targetText, ox, oy, f1, overlay.TargetFontFamily, targetColor, outlineColor, FontWeights.SemiBold);
            _timerWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)timerLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                tx, ty, f2, overlay.TimerFontFamily, outlineColor);
            _resistsWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)resistsLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                rx, ry,
                f3, overlay.TargetFontFamily, outlineColor);
            var cast = overlay.ShowCastBar ? snapshot.ActiveCast : null;
            _castBarWindow.Update(cast, cx, cy, castbarColor, timerColor, outlineColor, overlay.CastbarFontFamily,
                snapshot.CastInterruptedUntil);
            _groupWindow.Update(
                overlay.ShowGroup ? snapshot.ClientState?.GroupMembers : null,
                gx, gy, overlay.GroupSize, overlay.TimerFontFamily, outlineColor);

            _selfCcWindow.Update(
                overlay.ShowSelfCc && snapshot.SelfCc is not null
                    ? BuildSelfCcText(snapshot.SelfCc)
                    : string.Empty,
                scx, scy, overlay.SelfCcSize, overlay.TargetFontFamily,
                snapshot.SelfCc is { } cc ? EffectTypeColor(cc.Effect, Colors.White) : Colors.White,
                outlineColor, FontWeights.Bold);

            var peelLines = overlay.ShowPeel ? BuildPeelLines(snapshot.RecentAttackers) : null;
            _peelWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)peelLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                px, py, overlay.PeelSize, overlay.TimerFontFamily, outlineColor);

            var worldLines = overlay.ShowWorld ? BuildWorldTimerLines(snapshot.RecentRealmAbilityUses, snapshot.CombatStats) : null;
            _worldWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)worldLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                wx, wy, overlay.WorldSize, overlay.TimerFontFamily, outlineColor);

            var buffLines = overlay.ShowBuffs ? BuildBuffLines(snapshot.ClientState) : null;
            _buffWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)buffLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                bx, by, overlay.BuffSize, overlay.TimerFontFamily, outlineColor);

            var chatLines = overlay.ShowChat ? BuildChatLines(snapshot.RawOcrText) : null;
            _chatWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)chatLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                chx, chy, overlay.ChatSize, overlay.TimerFontFamily, outlineColor);
            Rendered?.Invoke(this, new OverlayViewState(
                targetText,
                timerText,
                timerLines,
                resistsLines,
                cast,
                targetColor,
                timerColor,
                outlineColor,
                overlay.TargetFontFamily,
                overlay.TimerFontFamily,
                f1,
                f2,
                f3,
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

    public void SetPreviewResistsFontSize(int size)
    {
        _previewResistsFontSize = Math.Clamp(size, 10, 72);
    }

    public void SetPreviewGroupPosition(int x, int y)
    {
        _previewGroupX = x;
        _previewGroupY = y;
    }

    public void SetPreviewSelfCcPosition(int x, int y)
    {
        _previewSelfCcX = x;
        _previewSelfCcY = y;
    }

    public void SetPreviewPeelPosition(int x, int y)
    {
        _previewPeelX = x;
        _previewPeelY = y;
    }

    public void SetPreviewWorldPosition(int x, int y)
    {
        _previewWorldX = x;
        _previewWorldY = y;
    }

    public void SetPreviewBuffPosition(int x, int y)
    {
        _previewBuffX = x;
        _previewBuffY = y;
    }

    public void SetPreviewChatPosition(int x, int y)
    {
        _previewChatX = x;
        _previewChatY = y;
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
        _previewResistsFontSize = null;
        _previewGroupX = null;
        _previewGroupY = null;
        _previewSelfCcX = null;
        _previewSelfCcY = null;
        _previewPeelX = null;
        _previewPeelY = null;
        _previewWorldX = null;
        _previewWorldY = null;
        _previewBuffX = null;
        _previewBuffY = null;
        _previewChatX = null;
        _previewChatY = null;
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
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildResistsLines(TargetProfile? target)
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
            ("Thrust", VerdictColor(thrust), (IconSpriteRef?)null),
            ("Slash", VerdictColor(slash), (IconSpriteRef?)null),
            ("Crush", VerdictColor(crush), (IconSpriteRef?)null),
        ];
    }

    /// <summary>Self-CC banner — the chat line has no duration, so the banner
    /// shows elapsed time ("STUNNED 4.2s") in the effect's color.</summary>
    internal static string BuildSelfCcText(SelfCcState selfCc)
    {
        var elapsed = Math.Max(0, (DateTimeOffset.UtcNow - selfCc.StartedUtc).TotalSeconds);
        var label = selfCc.Effect switch
        {
            ControlEffectType.Stun => "STUNNED",
            ControlEffectType.Mezz => "MESMERIZED",
            ControlEffectType.Root => "ROOTED",
            _ => "CC'd"
        };
        return $"{label}  {elapsed:0.0}s";
    }

    /// <summary>Peel list — who's been hitting you lately, hit count
    /// included ("Foo ×3"). Red like the danger it is.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildPeelLines(
        IReadOnlyCollection<PeelEntry>? attackers)
    {
        if (attackers is null || attackers.Count == 0)
        {
            return null;
        }

        var peelColor = MediaColor.FromRgb(0xE0, 0x5D, 0x65);
        return attackers
            .OrderByDescending(a => a.LastSeenUtc)
            .Take(6)
            .Select(a => ($"{a.Attacker}  ×{a.HitCount}", peelColor, (IconSpriteRef?)null))
            .ToList();
    }

    /// <summary>World window = session stats header + realm-ability cooldowns.
    /// (Siege/relic/release adapter widgets were removed — they only ever
    /// showed 0s placeholders without the matching client window open.)</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildWorldTimerLines(
        IReadOnlyCollection<RealmAbilityActivation>? raUses = null,
        SessionCombatStats? stats = null)
    {
        var lines = new List<(string, MediaColor, IconSpriteRef?)>();
        var raColor = MediaColor.FromRgb(0x9C, 0x6E, 0xE8);
        var statColor = MediaColor.FromRgb(0x8E, 0x9E, 0xB0);
        var now = DateTimeOffset.UtcNow;

        if (stats is not null)
        {
            var up = now - stats.StartedUtc;
            lines.Add((
                $"{up.TotalMinutes:0}m  K{stats.Kills} D{stats.Deaths}  hits {stats.HitsTaken}  RA {stats.RealmAbilityUses}",
                statColor, null));
        }

        if (raUses is not null)
        {
            foreach (var ra in raUses.OrderByDescending(x => x.UsedUtc).Take(6))
            {
                var elapsed = now - ra.UsedUtc;
                if (ra.CooldownSeconds is { } cooldown && elapsed.TotalSeconds < cooldown)
                {
                    var left = TimeSpan.FromSeconds(cooldown) - elapsed;
                    lines.Add(($"RA {ra.AbilityName} {left:m\\:ss}", raColor, null));
                }
                else if (elapsed.TotalMinutes < 30)
                {
                    lines.Add(($"RA {ra.AbilityName} {elapsed.TotalMinutes:0}m ago", raColor, null));
                }
            }
        }

        return lines.Count == 0 ? null : lines;
    }

    /// <summary>Buff list + pet vitals — concentration buffs orange, normal
    /// buffs cyan, pet green. Data is whatever the adapter feed reports.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildBuffLines(
        ClientStateSnapshot? state)
    {
        if (state is null)
        {
            return null;
        }

        var lines = new List<(string, MediaColor, IconSpriteRef?)>();
        var concColor = MediaColor.FromRgb(0xE7, 0xA9, 0x3A);
        var buffColor = MediaColor.FromRgb(0x4A, 0xC5, 0xE7);
        var petColor = MediaColor.FromRgb(0x45, 0xB9, 0x7C);

        if (state.Pet is { Title: { Length: > 0 } title } pet)
        {
            var life = pet.LifePercent is { } hp ? $" {hp}%" : string.Empty;
            lines.Add(($"Pet {title}{life}", petColor, null));
        }

        lines.AddRange(state.ConcentrationBuffs
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => (b, concColor, (IconSpriteRef?)null)));
        lines.AddRange(state.Buffs
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => (b, buffColor, (IconSpriteRef?)null)));

        return lines.Count == 0 ? null : lines;
    }

    /// <summary>Rolling chat feed — last lines of whatever the capture chain
    /// produced (mem/chat.log/relay = real chat; OCR = the dragged region).</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildChatLines(
        string? rawText, int maxLines = 8)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var neutral = MediaColor.FromRgb(0xC8, 0xC8, 0xC8);
        var lines = rawText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r', ' '))
            .Where(x => x.Length > 0)
            .TakeLast(maxLines)
            .Select(x => (x, neutral, (IconSpriteRef?)null))
            .ToList();
        return lines.Count == 0 ? null : lines;
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

    /// <summary>Timer lines colored by CC type — the AHK palette: mezz yellow,
    /// stun magenta, root amber; anything else takes the user's timer color.
    /// Each line can carry the ability's catalog icon (resolved lazily by the
    /// caller via IconImageLoader).</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildTimerLines(
        IReadOnlyCollection<CcTimerEntry> timers,
        MediaColor fallbackColor)
    {
        if (timers.Count == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        // Expiring-soon timers float to the top and blink — the render cadence
        // (~350ms) alternates them between the effect color and near-white.
        var flashOn = now.Millisecond < 500;
        return timers
            .OrderBy(x => x.RemainingSeconds(now))
            .ThenBy(x => x.TargetName, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var remaining = x.RemainingSeconds(now);
                var expiring = remaining is > 0 and < 3;
                var color = expiring && flashOn
                    ? Colors.White
                    : EffectTypeColor(x.EffectType, fallbackColor);
                return (
                    $"{(expiring ? "! " : "")}{ShortType(x.EffectType)} {x.TargetName}{ShortClassTag(x.TargetClass)} {remaining}",
                    color,
                    x.Icon);
            })
            .Take(12)
            .ToList();
    }

    private static MediaColor EffectTypeColor(ControlEffectType type, MediaColor fallback) =>
        type switch
        {
            ControlEffectType.Mezz => MediaColor.FromRgb(0xDC, 0xD3, 0x35),
            ControlEffectType.Stun => MediaColor.FromRgb(0xCF, 0x33, 0xA4),
            ControlEffectType.Root => MediaColor.FromRgb(0xA8, 0x71, 0x30),
            _ => fallback
        };

    /// <summary>" ·Cle" — 3-letter class tag when the herald profile resolved
    /// the target, so multi-target lines say who is stunned, not just a name.</summary>
    private static string ShortClassTag(string? targetClass) =>
        string.IsNullOrWhiteSpace(targetClass)
            ? string.Empty
            : $" ·{targetClass[..Math.Min(3, targetClass.Length)]}";

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
        _groupWindow.Close();
        _selfCcWindow.Close();
        _peelWindow.Close();
        _worldWindow.Close();
        _buffWindow.Close();
        _chatWindow.Close();
    }
}
