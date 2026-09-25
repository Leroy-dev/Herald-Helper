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
    private readonly OverlayTextWindow _buffWindow;
    private readonly OverlayTextWindow _petWindow;
    private readonly OverlayTextWindow _cooldownsWindow;
    /// <summary>Live drag/resize previews keyed by element ("target",
    /// "timers", "castbar", "resists", "group", "selfcc", "peel", "buffs",
    /// "pet") — the Overlay settings table drives these.</summary>
    private readonly Dictionary<string, (int X, int Y)> _previewPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _previewFontSizes = new(StringComparer.OrdinalIgnoreCase);
    private MediaColor? _previewTargetColor;
    private MediaColor? _previewTimerColor;
    private MediaColor? _previewOutlineColor;
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
        _buffWindow = new OverlayTextWindow();
        _petWindow = new OverlayTextWindow();
        _cooldownsWindow = new OverlayTextWindow();
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
            _buffWindow.Opacity = opacity;
            _petWindow.Opacity = opacity;
            _cooldownsWindow.Opacity = opacity;

            var (ox, oy) = PreviewPos("target", overlay.X, overlay.Y);
            var (tx, ty) = PreviewPos("timers", overlay.TimerX, overlay.TimerY);
            var (cx, cy) = PreviewPos("castbar", overlay.CastX, overlay.CastY);
            var (rx, ry) = PreviewPos("resists", overlay.ResistsX, overlay.ResistsY);
            var (gx, gy) = PreviewPos("group", overlay.GroupX, overlay.GroupY);
            var (scx, scy) = PreviewPos("selfcc", overlay.SelfCcX, overlay.SelfCcY);
            var (px, py) = PreviewPos("peel", overlay.PeelX, overlay.PeelY);
            var (bx, by) = PreviewPos("buffs", overlay.BuffX, overlay.BuffY);
            var (pex, pey) = PreviewPos("pet", overlay.PetX, overlay.PetY);
            var (cdx, cdy) = PreviewPos("cooldowns", overlay.CooldownsX, overlay.CooldownsY);

            var f1 = PreviewSize("target", overlay.FontSize);
            var f2 = PreviewSize("timers", overlay.TimerSize);
            var f3 = PreviewSize("resists", overlay.ResistsSize);
            var castSize = PreviewSize("castbar", overlay.CastSize);
            var groupSize = PreviewSize("group", overlay.GroupSize);
            var selfCcSize = PreviewSize("selfcc", overlay.SelfCcSize);
            var peelSize = PreviewSize("peel", overlay.PeelSize);
            var buffSize = PreviewSize("buffs", overlay.BuffSize);
            var petSize = PreviewSize("pet", overlay.EffectivePetSize);
            var cooldownsSize = PreviewSize("cooldowns", overlay.EffectiveCooldownsSize);

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

            var targetText = overlay.ShowTarget
                ? BuildTargetText(snapshot.Target, overlay, snapshot.ClientState?.TargetHealthPercent)
                : string.Empty;
            var timerLines = overlay.ShowTimers
                ? BuildTimerLines(snapshot.Timers, timerColor, snapshot.Target?.Name)
                : null;
            var cooldownLines = overlay.ShowCooldowns ? BuildCooldownLines(snapshot.Cooldowns) : null;
            var timerText = timerLines is null ? string.Empty : string.Join("\n", timerLines.Select(l => l.Text));
            var resistsLines = overlay.ShowResists ? BuildResistsLines(snapshot.Target, snapshot.ClientState) : null;

            _targetWindow.Update(targetText, ox, oy, f1, overlay.TargetFontFamily, targetColor, outlineColor, FontWeights.SemiBold);
            _timerWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)timerLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                tx, ty, f2, overlay.TimerFontFamily, outlineColor);
            _resistsWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)resistsLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                rx, ry,
                f3, overlay.EffectiveResistsFontFamily, outlineColor);
            var cast = overlay.ShowCastBar ? snapshot.ActiveCast : null;
            _castBarWindow.Update(cast, cx, cy, castbarColor, timerColor, outlineColor, overlay.CastbarFontFamily,
                castSize, snapshot.CastInterruptedUntil);
            _groupWindow.Update(
                overlay.ShowGroup ? snapshot.ClientState?.GroupMembers : null,
                gx, gy, groupSize, overlay.EffectiveGroupFontFamily, outlineColor);

            _selfCcWindow.Update(
                overlay.ShowSelfCc && snapshot.SelfCc is not null
                    ? BuildSelfCcText(snapshot.SelfCc)
                    : string.Empty,
                scx, scy, selfCcSize, overlay.EffectiveSelfCcFontFamily,
                snapshot.SelfCc is { } cc ? EffectTypeColor(cc.Effect, Colors.White) : Colors.White,
                outlineColor, FontWeights.Bold);

            var peelLines = overlay.ShowPeel ? BuildPeelLines(snapshot.RecentAttackers) : null;
            _peelWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)peelLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                px, py, peelSize, overlay.EffectivePeelFontFamily, outlineColor);

            var buffLines = overlay.ShowBuffs ? BuildBuffLines(snapshot.ClientState) : null;
            _buffWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)buffLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                bx, by, buffSize, overlay.EffectiveBuffFontFamily, outlineColor);
            var petLines = overlay.ShowPet ? BuildPetLines(snapshot.ClientState) : null;
            _petWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)petLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                pex, pey, petSize, overlay.EffectivePetFontFamily, outlineColor);
            _cooldownsWindow.Update(
                (IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>?)cooldownLines ?? Array.Empty<(string Text, MediaColor Color, IconSpriteRef? Icon)>(),
                cdx, cdy, cooldownsSize, overlay.EffectiveCooldownsFontFamily, outlineColor);
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

    private (int X, int Y) PreviewPos(string key, int fallbackX, int fallbackY)
    {
        return _previewPositions.TryGetValue(key, out var pos) ? pos : (fallbackX, fallbackY);
    }

    private int PreviewSize(string key, int fallback)
    {
        return _previewFontSizes.TryGetValue(key, out var size) ? size : fallback;
    }

    /// <summary>Live-drag preview for an element's position — cleared by
    /// <see cref="ClearPreview"/> once the drag is accepted or cancelled.</summary>
    public void SetPreviewPosition(string elementKey, int x, int y)
    {
        _previewPositions[elementKey] = (x, y);
    }

    public void SetPreviewFontSize(string elementKey, int size)
    {
        _previewFontSizes[elementKey] = Math.Clamp(size, 8, 72);
    }

    public void SetPreviewColors(MediaColor? targetColor, MediaColor? timerColor, MediaColor? outlineColor)
    {
        _previewTargetColor = targetColor;
        _previewTimerColor = timerColor;
        _previewOutlineColor = outlineColor;
    }

    public void ClearPreview()
    {
        _previewPositions.Clear();
        _previewFontSizes.Clear();
        _previewTargetColor = null;
        _previewTimerColor = null;
        _previewOutlineColor = null;
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
    internal static string BuildTargetText(
        TargetProfile? target, OverlaySettings overlay, int? targetHealthPercent = null)
    {
        return BuildTargetTextCore(
            target,
            overlay.ShowGuild,
            overlay.ShowClass,
            overlay.ShowLevel,
            overlay.ShowRealmRank,
            overlay.ShowSoloKills,
            allowDetailFallback: false,
            targetHealthPercent);
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
    /// green = target weak to it, red = resists, white = neutral. When the
    /// stats memory source is live the window also carries the player's own
    /// vitals and real resist values.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildResistsLines(
        TargetProfile? target,
        ClientStateSnapshot? state = null)
    {
        var lines = new List<(string Text, MediaColor Color, IconSpriteRef? Icon)>();
        var white = MediaColor.FromRgb(0xFF, 0xFF, 0xFF);

        if (state?.Vitals is { } vitals)
        {
            var parts = new List<string>();
            if (vitals.HealthPercent is { } hp)
            {
                parts.Add($"HP {hp}%");
            }
            if (vitals.PowerPercent is { } pw)
            {
                parts.Add($"PW {pw}%");
            }
            if (vitals.EndurancePercent is { } en)
            {
                parts.Add($"EN {en}%");
            }
            if (parts.Count > 0)
            {
                lines.Add((string.Join("  ", parts), white, null));
            }

            var melee = FormatResists(vitals.Resists, "thrust", "slash", "crush");
            var magic = FormatResists(
                vitals.Resists, "heat", "cold", "matter", "body", "spirit", "energy");
            if (melee is not null)
            {
                lines.Add((melee, white, null));
            }
            if (magic is not null)
            {
                lines.Add((magic, white, null));
            }
        }

        if (target is not null && IsRealPlayerTarget(target))
        {
            var (thrust, slash, crush) = ClassArmorTable.Lookup(target.Class);
            if (thrust != DamageVerdict.Neutral || slash != DamageVerdict.Neutral ||
                crush != DamageVerdict.Neutral)
            {
                lines.Add(("Thrust", VerdictColor(thrust), null));
                lines.Add(("Slash", VerdictColor(slash), null));
                lines.Add(("Crush", VerdictColor(crush), null));
            }
        }

        return lines.Count == 0 ? null : lines;
    }

    private static string? FormatResists(
        IReadOnlyDictionary<string, int> resists, params string[] keys)
    {
        var parts = keys
            .Where(resists.ContainsKey)
            .Select(k => $"{k[..3].ToUpperInvariant()} {resists[k]:+0;-0;0}%")
            .ToList();
        return parts.Count == 0 ? null : string.Join("  ", parts);
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
            ControlEffectType.Nearsight => "NEARSIGHTED",
            ControlEffectType.Snare => "SNARED",
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

    /// <summary>Pet vitals + active-effect names — whatever the adapter feed
    /// reports (summary icon grid when that window renders, mini_pet_* always
    /// while a pet is out). The client only exposes pet life as a percent —
    /// there is no absolute pet HP adapter.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildBuffLines(
        ClientStateSnapshot? state)
    {
        if (state is null)
        {
            return null;
        }

        var lines = new List<(string, MediaColor, IconSpriteRef?)>();
        var buffColor = MediaColor.FromRgb(0x4A, 0xC5, 0xE7);

        // Live self-effects from the client's EFFECTS array — real names +
        // icons, the same data the top-of-screen buff bar draws.
        foreach (var effect in state.SelfEffects)
        {
            lines.Add((effect.Name, buffColor,
                effect.IconId > 0 ? IconCatalog.FindBestForSpell(effect.IconId) : null));
        }

        lines.AddRange(state.Buffs
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => (b, buffColor, (IconSpriteRef?)null)));

        return lines.Count == 0 ? null : lines;
    }

    /// <summary>Pet window lines: pet vitals + one icon row per active effect
    /// (mini_pet_effectN iconIds). Pet-only window — player buffs live in the
    /// buff window.</summary>
    internal static IReadOnlyList<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildPetLines(
        ClientStateSnapshot? state)
    {
        if (state?.Pet is not { } pet)
        {
            return null;
        }

        var petColor = MediaColor.FromRgb(0x45, 0xB9, 0x7C);
        var mutedColor = MediaColor.FromRgb(0x8E, 0x9B, 0xB0);
        var lines = new List<(string, MediaColor, IconSpriteRef?)>();

        if (pet.Title is { Length: > 0 } title)
        {
            var life = pet.LifePercent is { } hp ? $" {hp}%" : string.Empty;
            lines.Add(($"Pet {title}{life}", petColor, null));
        }

        // mini_pet_effect ids are the client's own icon ids — resolve to the
        // charplan sprite when we can, and to the real spell name via the
        // client icons.csv→spells.csv link when the id maps to a spell.
        foreach (var iconId in pet.EffectIconIds)
        {
            var sprite = IconCatalog.FindBestForSpell(iconId);
            var name = ClientIconSpellMap.NameForIcon(iconId);
            if (sprite is not null || name is not null)
            {
                lines.Add((name ?? string.Empty, petColor, sprite));
            }
            else
            {
                lines.Add(($"pet fx {iconId}", mutedColor, null));
            }
        }

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
        bool allowDetailFallback,
        int? targetHealthPercent = null)
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
        if (targetHealthPercent is { } hp)
        {
            line2Parts.Add($"{hp}%");
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
    /// caller via IconImageLoader). Cooldowns live in their own window.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildTimerLines(
        IReadOnlyCollection<CcTimerEntry> timers,
        MediaColor fallbackColor = default,
        string? currentTargetName = null)
    {
        var now = DateTimeOffset.UtcNow;
        var lines = new List<(string Text, MediaColor Color, IconSpriteRef? Icon)>();
        // Expiring-soon timers float to the top and blink — the render cadence
        // (~350ms) alternates them between the effect color and near-white.
        var flashOn = now.Millisecond < 500;
        lines.AddRange(timers
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
            }));

        // Per-category readiness on the current target — the categories with
        // no active immunity entry are open for application right now.
        if (!string.IsNullOrWhiteSpace(currentTargetName))
        {
            var normalizedTarget = NormalizeTimerTargetName(currentTargetName);
            var immune = timers
                .Where(x => x.RemainingSeconds(now) > 0 &&
                            string.Equals(NormalizeTimerTargetName(x.TargetName), normalizedTarget,
                                StringComparison.OrdinalIgnoreCase))
                .Select(x => x.EffectType)
                .ToHashSet();
            var ready = ReadinessCategories.Where(c => !immune.Contains(c)).ToList();
            lines.Add((
                ready.Count == ReadinessCategories.Length
                    ? $"READY: {string.Join(' ', ReadinessCategories.Select(ReadinessType))}"
                    : ready.Count == 0
                        ? "READY: none"
                        : $"READY: {string.Join(' ', ready.Select(ReadinessType))}",
                MediaColor.FromRgb(0x4E, 0xC9, 0x7B),
                null));
        }

        return lines.Count == 0 ? null : lines.Take(14).ToList();
    }

    private static readonly ControlEffectType[] ReadinessCategories =
        [ControlEffectType.Stun, ControlEffectType.Mezz, ControlEffectType.Root, ControlEffectType.Nearsight];

    /// <summary>Same normalization as CcImmunityTracker — chat lines and the
    /// target adapter disagree on "the " prefixes and "---" suffixes.</summary>
    private static string NormalizeTimerTargetName(string name)
    {
        var trimmed = name.Trim().TrimEnd('-').TrimEnd();
        return trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..].TrimStart()
            : trimmed;
    }

    /// <summary>Spell recasts and realm-ability cooldowns — purple "⟳ name
    /// mm:ss" countdowns, newest first. Used-time only entries fade after
    /// 30 minutes.</summary>
    internal static List<(string Text, MediaColor Color, IconSpriteRef? Icon)>? BuildCooldownLines(
        IReadOnlyCollection<CooldownEntry>? cooldowns)
    {
        if (cooldowns is null || cooldowns.Count == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var cooldownColor = MediaColor.FromRgb(0x9C, 0x6E, 0xE8);
        var lines = new List<(string Text, MediaColor Color, IconSpriteRef? Icon)>();
        foreach (var cd in cooldowns.OrderByDescending(x => x.UsedUtc))
        {
            if (cd.ReadyUtc is { } ready && ready > now)
            {
                lines.Add(($"⟳ {cd.Name} {(ready - now):m\\:ss}", cooldownColor, cd.Icon));
            }
            else if (cd.ReadyUtc is null && now - cd.UsedUtc < TimeSpan.FromMinutes(30))
            {
                lines.Add(($"⟳ {cd.Name} {(now - cd.UsedUtc).TotalMinutes:0}m ago", cooldownColor, cd.Icon));
            }
        }

        return lines.Count == 0 ? null : lines.Take(14).ToList();
    }

    private static MediaColor EffectTypeColor(ControlEffectType type, MediaColor fallback) =>
        type switch
        {
            ControlEffectType.Mezz => MediaColor.FromRgb(0xDC, 0xD3, 0x35),
            ControlEffectType.Stun => MediaColor.FromRgb(0xCF, 0x33, 0xA4),
            ControlEffectType.Root => MediaColor.FromRgb(0xA8, 0x71, 0x30),
            ControlEffectType.Nearsight => MediaColor.FromRgb(0x3E, 0xA4, 0xDC),
            ControlEffectType.Snare => MediaColor.FromRgb(0x4E, 0xC9, 0x7B),
            _ => fallback
        };

    /// <summary>" ·Cle" — 3-letter class tag when the herald profile resolved
    /// the target, so multi-target lines say who is stunned, not just a name.</summary>
    private static string ShortClassTag(string? targetClass) =>
        string.IsNullOrWhiteSpace(targetClass)
            ? string.Empty
            : $" ·{targetClass[..Math.Min(3, targetClass.Length)]}";

    private static string ReadinessType(ControlEffectType type) =>
        type switch
        {
            ControlEffectType.Mezz => "Mezz",
            ControlEffectType.Stun => "Stun",
            ControlEffectType.Root => "Root",
            ControlEffectType.Nearsight => "Nearsight",
            _ => type.ToString()
        };

    private static string ShortType(ControlEffectType type)
    {
        return type switch
        {
            ControlEffectType.Mezz => "M",
            ControlEffectType.Stun => "S",
            ControlEffectType.Root => "R",
            ControlEffectType.Nearsight => "N",
            ControlEffectType.Snare => "E",
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
        _buffWindow.Close();
        _petWindow.Close();
    }
}
