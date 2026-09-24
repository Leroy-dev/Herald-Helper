using System.Diagnostics;
using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public sealed class GameLoopOrchestrator : IDisposable
{
    private static readonly TimeSpan FailedLookupRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IncompleteProfileRefreshDelay = TimeSpan.FromSeconds(30);
    private readonly IChatCaptureService _chatCaptureService;
    private readonly IChatEventParser _chatEventParser;
    private readonly ICastSpellCatalog _castSpellCatalog;
    private readonly IHeraldClientFactory _heraldClientFactory;
    private readonly ICcImmunityTracker _ccImmunityTracker;
    private readonly IOverlayRenderer _overlayRenderer;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly IReadOnlyList<OcrWatchRegion> _ocrWatchRegions;
    private readonly List<IHeraldProfileUpdateSource> _profileUpdateSources = [];
    private TargetProfile? _lastTarget;
    private string? _currentTargetName;
    private ShardType _currentTargetShard;
    private TargetMembership _currentTargetMembership = TargetMembership.Unknown;
    private DateTimeOffset _lastLookupAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastLookupStartedAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _nextTargetLookupAtUtc = DateTimeOffset.MinValue;
    private Task<TargetLookupResult>? _targetLookupTask;
    private CancellationTokenSource? _targetLookupCts;
    private long _targetGeneration;
    private readonly VisibleEventTracker _targetEventTracker = new();
    // Cast starts can leave the OCR viewport and reappear as ordinal 1 on the
    // next cast. One missing frame is enough to allow the same cast again.
    private readonly VisibleEventTracker _castEventTracker = new(missingFramesBeforeReset: 1);
    private readonly VisibleEventTracker _abilityEventTracker = new();
    private readonly VisibleEventTracker _selfCcTracker = new();
    private readonly VisibleEventTracker _incomingAttackTracker = new();
    private readonly VisibleEventTracker _lifeEventTracker = new();
    private readonly VisibleEventTracker _realmAbilityTracker = new();
    private readonly VisibleEventTracker _negationEventTracker = new();
    private SelfCcState? _selfCc;
    private readonly Dictionary<string, PeelEntry> _attackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RealmAbilityActivation> _realmAbilityUses = [];
    private readonly Dictionary<string, CooldownEntry> _spellCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _castInterruptedUntil;
    private readonly object _targetLock = new();
    private readonly string? _activeCharacterClass;
    private readonly int? _activeCharacterLevel;
    private readonly string _activeCharacterName;
    private readonly Action<CharacterStatsSnapshot>? _saveCharacterStats;
    private readonly bool _dynamicCastSpeed;
    private readonly bool _estimatedSpellDamage;
    private CharacterStatsSnapshot? _activeCharacterStats;
    private string? _pendingStatsKey;
    private int _pendingStatsObservations;
    private readonly IOcrReplaySink? _ocrReplaySink;
    private readonly ITargetProfileCache? _targetProfileCache;
    private readonly IOnlineSyncService? _onlineSync;
    private readonly IAdapterValueSource? _adapterValueSource;
    private readonly IAlertSound? _alertSound;
    private readonly IReadOnlyDictionary<string, int> _realmAbilityCooldowns;
    private CastBarState? _activeCast;

    public GameLoopOrchestrator(
        IChatCaptureService chatCaptureService,
        IChatEventParser chatEventParser,
        ICastSpellCatalog castSpellCatalog,
        IHeraldClientFactory heraldClientFactory,
        ICcImmunityTracker ccImmunityTracker,
        IOverlayRenderer overlayRenderer,
        IReadOnlyList<OcrWatchRegion>? ocrWatchRegions = null,
        IResponseDiagnostics? diagnostics = null,
        string? activeCharacterClass = null,
        int? activeCharacterLevel = null,
        string? activeCharacterName = null,
        Func<CharacterStatsSnapshot?>? loadCharacterStats = null,
        Action<CharacterStatsSnapshot>? saveCharacterStats = null,
        bool dynamicCastSpeed = false,
        bool estimatedSpellDamage = false,
        IOcrReplaySink? ocrReplaySink = null,
        ITargetProfileCache? targetProfileCache = null,
        IOnlineSyncService? onlineSync = null,
        IAdapterValueSource? adapterValueSource = null,
        IAlertSound? alertSound = null,
        IReadOnlyDictionary<string, int>? realmAbilityCooldowns = null)
    {
        _chatCaptureService = chatCaptureService;
        _chatEventParser = chatEventParser;
        _castSpellCatalog = castSpellCatalog;
        _heraldClientFactory = heraldClientFactory;
        _ccImmunityTracker = ccImmunityTracker;
        _overlayRenderer = overlayRenderer;
        _ocrWatchRegions = ocrWatchRegions ?? [];
        _diagnostics = diagnostics;
        _activeCharacterClass = activeCharacterClass;
        _activeCharacterLevel = activeCharacterLevel;
        _activeCharacterName = activeCharacterName?.Trim() ?? string.Empty;
        _activeCharacterStats = loadCharacterStats?.Invoke();
        _saveCharacterStats = saveCharacterStats;
        _dynamicCastSpeed = dynamicCastSpeed;
        _estimatedSpellDamage = estimatedSpellDamage;
        _ocrReplaySink = ocrReplaySink;
        _targetProfileCache = targetProfileCache;
        _onlineSync = onlineSync;
        _adapterValueSource = adapterValueSource;
        _alertSound = alertSound;
        _realmAbilityCooldowns = realmAbilityCooldowns ?? new Dictionary<string, int>(0);

        foreach (var shard in Enum.GetValues<ShardType>())
        {
            var client = _heraldClientFactory.Resolve(shard);
            if (client is IHeraldProfileUpdateSource updates)
            {
                if (_profileUpdateSources.Contains(updates))
                {
                    continue;
                }

                _profileUpdateSources.Add(updates);
                updates.TargetProfileUpdated += OnTargetProfileUpdated;
            }
        }
    }

    public async Task TickAsync(
        ScreenRegion region,
        ShardType shardType,
        int resistPercent,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var tickStopwatch = Stopwatch.StartNew();
        var frame = await CaptureFrameAsync(region, shardType, cancellationToken);
        ObserveCharacterStats(frame.OcrText, shardType, nowUtc);
        MergeAdapterStats(shardType, nowUtc);
        var parseResult = ParseFrame(frame, shardType, nowUtc);
        TrackCastEvents(parseResult, nowUtc);
        ApplyCompletedTargetLookup(nowUtc);
        TrackTargetEvents(parseResult, shardType, nowUtc, cancellationToken);
        TrackAbilityHits(parseResult, resistPercent, nowUtc);
        TrackNegationEvents(parseResult, nowUtc);
        TrackCombatEvents(parseResult, nowUtc);
        await RenderFrameAsync(frame.OcrText, nowUtc, cancellationToken);
        tickStopwatch.Stop();
        _diagnostics?.Log($"[Timing] tick: {tickStopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>Logs chat lines the capture layer actually saw — once per
    /// distinct line so OCR regions re-emitting the whole window every tick
    /// don't drown the diagnostics view. The real question users ask is
    /// "did my Slam line even arrive" and nothing answered that before.</summary>
    private void LogNewChatLines(string text, string label)
    {
        if (_diagnostics is null)
        {
            return;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.Length > 200)
            {
                continue;
            }

            if (_seenChatLines.Add(line))
            {
                _seenChatLinesQueue.Enqueue(line);
                _diagnostics.Log($"[chat] {label}: {line}");
            }
        }

        while (_seenChatLinesQueue.Count > 400)
        {
            _seenChatLines.Remove(_seenChatLinesQueue.Dequeue());
        }
    }

    /// <summary>When stats memory read is live, the stats_* adapters carry the
    /// real (buffed) character-sheet numbers — merge them into the stats
    /// snapshot so dynamic cast speed and damage estimation don't depend on
    /// the character-stats OCR window ever having been open. The configured
    /// casting-speed/spell-damage bonuses ride along untouched.</summary>
    private void MergeAdapterStats(ShardType shardType, DateTimeOffset nowUtc)
    {
        var values = _adapterValueSource?.LatestAdapterValues;
        if (values is null
            || !int.TryParse(
                values.GetValueOrDefault("stats_dexterity")?.Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var dex))
        {
            return; // stats window never rendered — adapters stay empty
        }

        int? Num(string key) =>
            values.TryGetValue(key, out var raw) &&
            int.TryParse(raw.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var n)
                ? n
                : null;

        var current = _activeCharacterStats;
        var name = values.TryGetValue("stats_name", out var rawName) &&
                   !string.IsNullOrWhiteSpace(rawName)
            ? rawName.Trim()
            : current?.CharacterName ?? _activeCharacterName;
        var merged = (current ?? new CharacterStatsSnapshot(
                shardType, name, null, null, null, null, null, null, null, null, 0, 0, nowUtc)) with
        {
            Shard = shardType,
            CharacterName = name,
            Strength = Num("stats_strength") ?? current?.Strength,
            Constitution = Num("stats_constitution") ?? current?.Constitution,
            Dexterity = dex,
            Quickness = Num("stats_quickness") ?? current?.Quickness,
            Intelligence = Num("stats_intelligence") ?? current?.Intelligence,
            Piety = Num("stats_piety") ?? current?.Piety,
            Empathy = Num("stats_empathy") ?? current?.Empathy,
            Charisma = Num("stats_charisma") ?? current?.Charisma
        };

        // Records compare by value — same stats, same timestamp → no save.
        if (merged == current)
        {
            return;
        }

        _activeCharacterStats = merged with { UpdatedUtc = nowUtc };
        _saveCharacterStats?.Invoke(_activeCharacterStats);
        _diagnostics?.Log(
            $"[Stats] adapters | dex={dex} str={merged.Strength?.ToString() ?? "-"} " +
            $"con={merged.Constitution?.ToString() ?? "-"} qui={merged.Quickness?.ToString() ?? "-"}");
    }

    private readonly HashSet<string> _seenChatLines = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenChatLinesQueue = new();

    private sealed record FrameCapture(string OcrText, List<OcrReplayCapture> ReplayCaptures);

    private async Task<FrameCapture> CaptureFrameAsync(
        ScreenRegion region,
        ShardType shardType,
        CancellationToken cancellationToken)
    {
        var configuredCaptureRegions = _ocrWatchRegions.Count > 0;
        var captureRegions = _ocrWatchRegions.ToList();
        if (captureRegions.Count == 0 ||
            captureRegions.All(x => string.Equals(x.Key, "character-stats", StringComparison.OrdinalIgnoreCase)))
        {
            captureRegions.Insert(0, new OcrWatchRegion("chat", "Chat", region));
        }

        var ocrSegments = new List<string>();
        var replayCaptures = new List<OcrReplayCapture>();
        var batchDiagnostics = _chatCaptureService as IOcrCaptureBatchDiagnostics;
        batchDiagnostics?.BeginCaptureBatch();
        try
        {
            foreach (var captureRegion in captureRegions)
            {
                var regionStopwatch = Stopwatch.StartNew();
                try
                {
                    _diagnostics?.Log($"[OCR] capturing {captureRegion.Label} ({captureRegion.Region.X},{captureRegion.Region.Y},{captureRegion.Region.Width},{captureRegion.Region.Height})");
                    var text = _chatCaptureService is IWindowAwareChatCaptureService windowAwareCapture
                        ? await windowAwareCapture.CaptureWindowTextAsync(captureRegion, shardType, cancellationToken)
                        : await _chatCaptureService.CaptureChatTextAsync(captureRegion.Region, cancellationToken);
                    _diagnostics?.Log($"[OCR] {captureRegion.Label}: {text.Length} chars");
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        LogNewChatLines(text, captureRegion.Label);
                        ocrSegments.Add(configuredCaptureRegions
                            ? $"[{captureRegion.Label}]\n{text}"
                            : text);
                    }
                    if (_ocrReplaySink is not null &&
                        _chatCaptureService is IOcrCaptureSnapshotSource replaySource &&
                        replaySource.LastCapturePng is { Length: > 0 } png)
                    {
                        replayCaptures.Add(new OcrReplayCapture(
                            captureRegion.Label,
                            captureRegion.Region,
                            png.ToArray(),
                            text,
                            replaySource.LastCaptureEngineName));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _diagnostics?.Log($"[OCR] {captureRegion.Label} failed: {ex.Message}");
                }
                finally
                {
                    _diagnostics?.Log($"[Timing] capture {captureRegion.Label}: {regionStopwatch.ElapsedMilliseconds} ms");
                }
            }
        }
        finally
        {
            batchDiagnostics?.CompleteCaptureBatch();
        }

        return new FrameCapture(
            string.Join(Environment.NewLine + Environment.NewLine, ocrSegments),
            replayCaptures);
    }

    private ChatParseResult ParseFrame(FrameCapture frame, ShardType shardType, DateTimeOffset nowUtc)
    {
        var parseStopwatch = Stopwatch.StartNew();
        string? fallbackTarget;
        lock (_targetLock)
        {
            fallbackTarget = _currentTargetName;
        }
        // The live adapter registry knows the real target even when no
        // "you target" line has been seen (loop started mid-fight).
        fallbackTarget ??= ReadAdapterTargetName();
        var parseResult = _chatEventParser.Parse(frame.OcrText, fallbackTarget);
        parseStopwatch.Stop();
        _diagnostics?.Log($"[Timing] parse: {parseStopwatch.ElapsedMilliseconds} ms");

        var replayStopwatch = Stopwatch.StartNew();
        _ocrReplaySink?.Record(shardType, _activeCharacterName, nowUtc, frame.ReplayCaptures, parseResult);
        replayStopwatch.Stop();
        _diagnostics?.Log($"[Timing] replay write: {replayStopwatch.ElapsedMilliseconds} ms");
        return parseResult;
    }

    private string? ReadAdapterTargetName()
    {
        var values = _adapterValueSource?.LatestAdapterValues;
        if (values is null ||
            !values.TryGetValue("summary_target", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // summary_target renders as a small block — the name is the first line.
        var name = raw.Split('\n', '\r')[0].Trim().Trim('"', '\'', '.', ',');
        return name.Length is >= 2 and <= 40 && !name.Contains('=') ? name : null;
    }

    private void TrackCastEvents(ChatParseResult parseResult, DateTimeOffset nowUtc)
    {
        var visibleCastEvents = parseResult.VisibleCastEvents
            ?? (parseResult.CastEvent is null ? [] : [parseResult.CastEvent]);
        var newCastEvents = _castEventTracker.ObserveFrame(
            visibleCastEvents,
            BuildCastEventKey,
            static x => x.OccurrenceOrdinal);
        foreach (var castEvent in newCastEvents)
        {
            if (castEvent.EventType == CastEventType.Completed)
            {
                OnCastCompleted(castEvent, nowUtc);
            }
        }

        UpdateActiveCast(newCastEvents.LastOrDefault(), nowUtc);
    }

    /// <summary>A finished cast starts its recast cooldown (when the catalog
    /// knows one) and promotes realm abilities — some shards print RA use as
    /// a normal cast line.</summary>
    private void OnCastCompleted(CastEvent castEvent, DateTimeOffset nowUtc)
    {
        var spellName = NormalizeCastSpellName(castEvent.SpellName);
        if (string.IsNullOrWhiteSpace(spellName))
        {
            return;
        }

        if (_realmAbilityCooldowns.TryGetValue(spellName, out var raCooldown)
            && !_realmAbilityUses.Any(x =>
                string.Equals(x.AbilityName, spellName, StringComparison.OrdinalIgnoreCase)
                && nowUtc - x.UsedUtc < TimeSpan.FromSeconds(10)))
        {
            _realmAbilityUses.Add(new RealmAbilityActivation(spellName, nowUtc, raCooldown));
            _diagnostics?.Log($"[RA] {spellName} (via cast line)");
        }

        var spellInfo = _castSpellCatalog.FindBySpellName(spellName, _activeCharacterClass, _activeCharacterLevel);
        if (spellInfo?.RecastSeconds is > 0)
        {
            _spellCooldowns[spellInfo.SpellName] = new CooldownEntry(
                spellInfo.SpellName,
                nowUtc,
                nowUtc.AddSeconds(spellInfo.RecastSeconds.Value),
                spellInfo.Icon);
            _diagnostics?.Log($"[Cooldown] {spellInfo.SpellName} ready in {spellInfo.RecastSeconds.Value:0}s");
        }
    }

    private void TrackTargetEvents(
        ChatParseResult parseResult,
        ShardType shardType,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var visibleTargetEvents = parseResult.VisibleTargetEvents
            ?? (parseResult.TargetEvent is null ? [] : [parseResult.TargetEvent]);
        var newTargetEvents = _targetEventTracker.ObserveFrame(
            visibleTargetEvents,
            static x => x.Name.Trim(),
            static x => x.OccurrenceOrdinal);
        var targetEvent = newTargetEvents.LastOrDefault();
        if (targetEvent is null
            && parseResult.TargetEvent is not null
            && IsCurrentTarget(parseResult.TargetEvent.Name))
        {
            // Repeated observations of the current target still drive delayed herald retries.
            targetEvent = parseResult.TargetEvent;
        }

        // The adapter registry reports the live selection — a loop started
        // mid-fight (or chat that never showed the "you target" line) still
        // resolves the target. HandleTargetEvent dedupes repeats.
        if (targetEvent is null && ReadAdapterTargetName() is { } adapterTarget)
        {
            targetEvent = new TargetEvent(adapterTarget, TargetMembership.Unknown);
        }

        if (targetEvent is not null)
        {
            HandleTargetEvent(targetEvent, shardType, nowUtc, cancellationToken);
            ApplyCompletedTargetLookup(nowUtc);
        }
    }

    private void TrackAbilityHits(ChatParseResult parseResult, int resistPercent, DateTimeOffset nowUtc)
    {
        var newAbilityHits = _abilityEventTracker.ObserveFrame(
            parseResult.AbilityHits,
            BuildAbilityEventKey,
            static x => x.OccurrenceOrdinal);
        foreach (var hit in newAbilityHits)
        {
            _ccImmunityTracker.RegisterSuccessfulHit(hit, GetTargetClass(hit.TargetName), resistPercent, nowUtc);
            if (hit.LandedSuccessfully)
            {
                _diagnostics?.Log(
                    $"[CC] {hit.AbilityName} → {hit.TargetName} ({hit.EffectType}, {hit.BaseDurationSeconds}s)");
            }
        }
    }

    /// <summary>Resists, immunity, and failed swings arrive in the same frame
    /// as the "You cast/perform" line — or scroll in a tick later. Retract
    /// only freshly created timers so an old immunity window survives a
    /// brand-new resisted attempt.</summary>
    private void TrackNegationEvents(ChatParseResult parseResult, DateTimeOffset nowUtc)
    {
        var newNegations = _negationEventTracker.ObserveFrame(
            parseResult.NegationEvents ?? [],
            static x => $"{x.Kind}|{x.TargetName ?? string.Empty}",
            static x => x.OccurrenceOrdinal);
        foreach (var negation in newNegations)
        {
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(negation.TargetName))
            {
                names.Add(negation.TargetName);
            }
            else
            {
                lock (_targetLock)
                {
                    if (!string.IsNullOrWhiteSpace(_currentTargetName))
                    {
                        names.Add(_currentTargetName);
                    }
                }
            }
            if (names.Count == 0)
            {
                continue;
            }

            // A resisted/immune spell can lag the cast line by a frame; a
            // failed swing must only undo the timer its own perform created.
            // A rejected application ("already has this effect") means the
            // prior timer is still valid — only a timer created by this
            // exact attempt (a frame earlier) may be retracted.
            var maxAge = negation.Kind switch
            {
                NegationKind.SwingFailed or NegationKind.StyleFailed => TimeSpan.FromSeconds(3),
                NegationKind.FailedApplication => TimeSpan.FromSeconds(1.5),
                _ => TimeSpan.FromSeconds(8)
            };
            _ccImmunityTracker.RetractFreshEntries(names, nowUtc, maxAge);
            _diagnostics?.Log(
                $"[CC] {negation.Kind} → retracted fresh timers for {string.Join(", ", names)}");
        }
    }

    /// <summary>Self-CC, incoming attacks, kills/deaths, and realm-ability
    /// activations — deduped like the other chat events so a line that stays
    /// on screen doesn't re-fire every tick.</summary>
    private void TrackCombatEvents(ChatParseResult parseResult, DateTimeOffset nowUtc)
    {
        foreach (var cc in _selfCcTracker.ObserveFrame(
                     parseResult.SelfCcEvents ?? [],
                     static x => x.Effect.ToString(),
                     static x => x.OccurrenceOrdinal))
        {
            _selfCc = new SelfCcState(cc.Effect, nowUtc);
            _diagnostics?.Log($"[SelfCC] {cc.Effect}");
            _alertSound?.Play(AlertKind.SelfCc);
        }

        foreach (var attack in _incomingAttackTracker.ObserveFrame(
                     parseResult.IncomingAttacks ?? [],
                     static x => x.Attacker,
                     static x => x.OccurrenceOrdinal))
        {
            _attackers.TryGetValue(attack.Attacker, out var entry);
            _attackers[attack.Attacker] = new PeelEntry(
                attack.Attacker, (entry?.HitCount ?? 0) + 1, nowUtc);
            if (entry is null)
            {
                // First sighting of this attacker — that's the alert moment,
                // not every swing afterwards.
                _alertSound?.Play(AlertKind.IncomingAttack);
            }
            _diagnostics?.Log($"[Combat] {attack.Attacker} hit you" +
                              (attack.Damage is { } dmg ? $" for {dmg}" : "") +
                              (attack.IsCritical ? " (crit)" : "") +
                              (attack.Missed ? " (missed)" : ""));
        }

        foreach (var life in _lifeEventTracker.ObserveFrame(
                     parseResult.LifeEvents ?? [],
                     static x => $"{x.Kind}|{x.OtherName}",
                     static x => x.OccurrenceOrdinal))
        {
            if (life.Kind == CombatLifeKind.Kill)
            {
                _diagnostics?.Log($"[Combat] you killed {life.OtherName}");
            }
            else
            {
                _diagnostics?.Log($"[Combat] you died" + (life.OtherName is { } killer ? $" to {killer}" : ""));
            }
        }

        foreach (var ra in _realmAbilityTracker.ObserveFrame(
                     parseResult.RealmAbilityEvents ?? [],
                     static x => x.AbilityName,
                     static x => x.OccurrenceOrdinal))
        {
            _realmAbilityUses.Add(new RealmAbilityActivation(
                ra.AbilityName, nowUtc,
                _realmAbilityCooldowns.TryGetValue(ra.AbilityName, out var cooldown) ? cooldown : null));
            _diagnostics?.Log($"[RA] {ra.AbilityName}");
        }
    }

    private async Task RenderFrameAsync(string ocrText, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var timers = _ccImmunityTracker.GetActiveTimers(nowUtc);
        if (_activeCast is not null && !_activeCast.IsActive(nowUtc))
        {
            _activeCast = null;
        }

        // Self-CC: no duration in chat — keep the banner for the longest
        // realistic CC (~90s), then drop it. Attackers fade after 10s.
        if (_selfCc is not null && nowUtc - _selfCc.StartedUtc > TimeSpan.FromSeconds(90))
        {
            _selfCc = null;
        }
        foreach (var stale in _attackers
                     .Where(x => nowUtc - x.Value.LastSeenUtc > TimeSpan.FromSeconds(10))
                     .Select(x => x.Key).ToList())
        {
            _attackers.Remove(stale);
        }
        // Drop RA uses once their cooldown expired — unknown cooldowns keep a
        // 30-minute shelf life so the list can't grow forever.
        _realmAbilityUses.RemoveAll(x =>
            nowUtc - x.UsedUtc > TimeSpan.FromSeconds(x.CooldownSeconds ?? 1800));
        foreach (var ready in _spellCooldowns.Where(x => x.Value.ReadyUtc <= nowUtc)
                     .Select(x => x.Key).ToList())
        {
            _spellCooldowns.Remove(ready);
        }

        var snapshot = new OverlaySnapshot(
            GetLastTarget(),
            timers,
            _activeCast,
            ocrText,
            _selfCc,
            _attackers.Values.OrderByDescending(x => x.LastSeenUtc).Take(6).ToList(),
            BuildCooldownLines(),
            ClientStateExtractor.Extract(_adapterValueSource?.LatestAdapterValues),
            _castInterruptedUntil > nowUtc ? _castInterruptedUntil : null);

        var renderStopwatch = Stopwatch.StartNew();
        await _overlayRenderer.RenderAsync(snapshot, cancellationToken);
        renderStopwatch.Stop();
        _diagnostics?.Log($"[Timing] render: {renderStopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>RA activations + spell recasts as one countdown list —
    /// ready-time when the cooldown is known, else used-time only.</summary>
    private List<CooldownEntry> BuildCooldownLines()
    {
        return _realmAbilityUses
            .Select(x => new CooldownEntry(
                x.AbilityName,
                x.UsedUtc,
                x.CooldownSeconds is { } cd ? x.UsedUtc.AddSeconds(cd) : null))
            .Concat(_spellCooldowns.Values)
            .OrderByDescending(x => x.UsedUtc)
            .Take(12)
            .ToList();
    }

    private void UpdateActiveCast(CastEvent? castEvent, DateTimeOffset nowUtc)
    {
        if (castEvent is null)
        {
            return;
        }

        if (castEvent.EventType == CastEventType.Completed)
        {
            var completedSpellName = NormalizeCastSpellName(castEvent.SpellName);
            if (_activeCast is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(completedSpellName) ||
                string.Equals(_activeCast.SpellName, completedSpellName, StringComparison.OrdinalIgnoreCase))
            {
                _activeCast = null;
            }

            return;
        }

        if (castEvent.EventType == CastEventType.Interrupted)
        {
            _activeCast = null;
            _castInterruptedUntil = nowUtc.AddSeconds(1.4);
            _diagnostics?.Log("[Cast] interrupted");
            _alertSound?.Play(AlertKind.CastInterrupted);
            return;
        }

        var spellName = NormalizeCastSpellName(castEvent.SpellName);
        if (string.IsNullOrWhiteSpace(spellName))
        {
            return;
        }

        if (_activeCast is not null &&
            _activeCast.IsActive(nowUtc) &&
            string.Equals(_activeCast.SpellName, spellName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var spellInfo = _castSpellCatalog.FindBySpellName(spellName, _activeCharacterClass, _activeCharacterLevel);
        if (spellInfo is null || spellInfo.CastTimeSeconds <= 0)
        {
            _diagnostics?.Log($"[Cast] no cast metadata for {spellName}");
            return;
        }

        var adjustedCastTime = CastMetricsCalculator.CalculateCastTime(
            spellInfo.CastTimeSeconds,
            _activeCharacterStats,
            _dynamicCastSpeed,
            spellInfo.IsFixedCastTime);
        var estimatedDamage = CastMetricsCalculator.EstimateDamage(
            spellInfo,
            _activeCharacterStats,
            _activeCharacterClass,
            _estimatedSpellDamage);
        _activeCast = new CastBarState(
            spellInfo.SpellName,
            adjustedCastTime,
            nowUtc,
            nowUtc.AddSeconds(adjustedCastTime),
            spellInfo.Icon,
            estimatedDamage,
            spellInfo.DamageType,
            spellInfo.CastTimeSeconds);
    }

    private void ObserveCharacterStats(string ocrText, ShardType shard, DateTimeOffset nowUtc)
    {
        var parsed = CharacterStatsParser.Parse(
            ocrText,
            shard,
            _activeCharacterName,
            _activeCharacterStats,
            nowUtc);
        if (parsed is null)
        {
            _pendingStatsKey = null;
            _pendingStatsObservations = 0;
            return;
        }

        var key = CharacterStatsParser.StabilityKey(parsed);
        if (!string.Equals(key, _pendingStatsKey, StringComparison.Ordinal))
        {
            _pendingStatsKey = key;
            _pendingStatsObservations = 1;
            return;
        }
        _pendingStatsObservations++;
        if (_pendingStatsObservations < 2 ||
            string.Equals(CharacterStatsParser.StabilityKey(_activeCharacterStats ?? parsed), key, StringComparison.Ordinal) &&
            _activeCharacterStats is not null)
        {
            return;
        }

        _activeCharacterStats = parsed;
        _saveCharacterStats?.Invoke(parsed);
        _diagnostics?.Log(
            $"[Stats] {_activeCharacterName} updated | dex={parsed.Dexterity?.ToString() ?? "-"} " +
            $"cast-speed={parsed.CastingSpeedPercent:0.##}% spell-damage={parsed.SpellDamagePercent:0.##}%");
    }

    private static string NormalizeCastSpellName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim();
        normalized = Regex.Replace(normalized, @"^(?:a|an|the)\s+", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+spell$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }

    private static string BuildCastEventKey(CastEvent castEvent)
    {
        return $"{castEvent.EventType}|{NormalizeCastSpellName(castEvent.SpellName)}";
    }

    private static string BuildAbilityEventKey(AbilityHit hit)
    {
        return $"{hit.TargetName}|{hit.AbilityName}|{hit.SkillCode}|{hit.EffectType}|{hit.LandedSuccessfully}";
    }

    private bool IsCurrentTarget(string targetName)
    {
        lock (_targetLock)
        {
            return string.Equals(_currentTargetName, targetName.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private void HandleTargetEvent(
        TargetEvent targetEvent,
        ShardType shardType,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var targetName = targetEvent.Name.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return;
        }

        CancellationTokenSource? previousLookup = null;
        var logNonMember = false;
        var startedLookup = false;

        lock (_targetLock)
        {
            var targetChanged = !string.Equals(targetName, _currentTargetName, StringComparison.OrdinalIgnoreCase)
                || shardType != _currentTargetShard;

            if (targetEvent.Membership == TargetMembership.NonMember)
            {
                if (targetChanged || _currentTargetMembership != TargetMembership.NonMember)
                {
                    previousLookup = ResetCurrentTargetLocked(targetName, shardType, TargetMembership.NonMember);
                    _nextTargetLookupAtUtc = DateTimeOffset.MaxValue;
                    logNonMember = true;
                }
            }
            else if (targetChanged || _currentTargetMembership == TargetMembership.NonMember)
            {
                previousLookup = ResetCurrentTargetLocked(targetName, shardType, targetEvent.Membership);
                RestoreCachedPlayerTargetLocked(targetEvent, shardType);
                SetPendingPlayerTargetLocked(targetEvent);
                startedLookup = StartTargetLookupLocked(targetName, shardType, cancellationToken);
            }
            else
            {
                if (targetEvent.Membership == TargetMembership.Member &&
                    _currentTargetMembership != TargetMembership.Member)
                {
                    _currentTargetMembership = TargetMembership.Member;
                    RestoreCachedPlayerTargetLocked(targetEvent, shardType);
                    SetPendingPlayerTargetLocked(targetEvent);
                }

                var displayedTarget = _lastTarget;
                var hasCurrentTargetProfile = displayedTarget is not null
                    && string.Equals(displayedTarget.Name, targetName, StringComparison.OrdinalIgnoreCase);
                var shouldRetryMissingProfile = !hasCurrentTargetProfile && nowUtc >= _nextTargetLookupAtUtc;
                var shouldRefreshIncompleteProfile = displayedTarget is not null
                    && hasCurrentTargetProfile
                    && IsIncomplete(displayedTarget)
                    && nowUtc - _lastLookupAtUtc >= IncompleteProfileRefreshDelay;
                if (_targetLookupTask is null && (shouldRetryMissingProfile || shouldRefreshIncompleteProfile))
                {
                    startedLookup = StartTargetLookupLocked(targetName, shardType, cancellationToken);
                }
            }
        }

        previousLookup?.Cancel();
        previousLookup?.Dispose();

        if (logNonMember)
        {
            _diagnostics?.Log($"[Target] ignored non-member target: {targetName}; keeping last resolved player.");
        }
        else if (startedLookup)
        {
            var membership = targetEvent.Membership.ToString().ToLowerInvariant();
            _diagnostics?.Log($"[Target] resolving {targetName} asynchronously | membership={membership}");
        }
    }

    private CancellationTokenSource? ResetCurrentTargetLocked(
        string targetName,
        ShardType shardType,
        TargetMembership membership)
    {
        var previousLookup = _targetLookupCts;
        _targetLookupCts = null;
        _targetLookupTask = null;
        _targetGeneration++;
        _currentTargetName = targetName;
        _currentTargetShard = shardType;
        _currentTargetMembership = membership;
        _lastLookupAtUtc = DateTimeOffset.MinValue;
        _nextTargetLookupAtUtc = DateTimeOffset.MinValue;
        return previousLookup;
    }

    private void SetPendingPlayerTargetLocked(TargetEvent targetEvent)
    {
        if (targetEvent.Membership != TargetMembership.Member ||
            string.Equals(_lastTarget?.Name, targetEvent.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Show the confirmed player name immediately. The herald result fills
        // class, guild, rank, and solo kills without blocking the overlay.
        _lastTarget = new TargetProfile(targetEvent.Name, null, null, null, null, null) { IsLoading = true };
    }

    private void RestoreCachedPlayerTargetLocked(TargetEvent targetEvent, ShardType shardType)
    {
        if (targetEvent.Membership != TargetMembership.Member || _targetProfileCache is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var cached = _targetProfileCache.Load(shardType, targetEvent.Name);
            if (cached is not null)
            {
                if (!string.Equals(cached.Name, targetEvent.Name, StringComparison.OrdinalIgnoreCase))
                {
                    cached = cached with { Name = targetEvent.Name };
                }

                _lastTarget = cached;
                _diagnostics?.Log($"[Target] cache hit for {cached.Name}; refreshing in background.");
            }
        }
        catch (Exception ex)
        {
            _diagnostics?.Log($"[Target] cache read failed for {targetEvent.Name}: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            _diagnostics?.Log($"[Timing] cache read: {stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private bool StartTargetLookupLocked(
        string targetName,
        ShardType shardType,
        CancellationToken cancellationToken)
    {
        if (_targetLookupTask is not null)
        {
            return false;
        }

        var client = _heraldClientFactory.Resolve(shardType);
        _lastLookupStartedAtUtc = DateTimeOffset.UtcNow;
        _targetLookupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _targetLookupTask = LookupTargetAsync(
            client,
            _onlineSync,
            targetName,
            shardType,
            _targetGeneration,
            _targetLookupCts.Token);
        return true;
    }

    private static async Task<TargetLookupResult> LookupTargetAsync(
        IHeraldClient client,
        IOnlineSyncService? onlineSync,
        string targetName,
        ShardType shardType,
        long generation,
        CancellationToken cancellationToken)
    {
        if (onlineSync is not null)
        {
            try
            {
                var onlineResult = await onlineSync.TryDownloadAsync(shardType, targetName, cancellationToken).ConfigureAwait(false);
                if (onlineResult.Profile is not null)
                {
                    return new TargetLookupResult(targetName, shardType, generation, onlineResult.Profile, null, false);
                }
            }
            catch
            {
                // Swallow online lookup errors and fall through to herald.
            }
        }

        try
        {
            var profile = await client.GetTargetProfileAsync(targetName, cancellationToken).ConfigureAwait(false);
            return new TargetLookupResult(targetName, shardType, generation, profile, null, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new TargetLookupResult(targetName, shardType, generation, null, null, true);
        }
        catch (Exception ex)
        {
            return new TargetLookupResult(targetName, shardType, generation, null, ex, false);
        }
    }

    private void ApplyCompletedTargetLookup(DateTimeOffset nowUtc)
    {
        TargetLookupResult result;
        CancellationTokenSource? completedLookupCts;
        TargetProfile? profileToCache = null;
        lock (_targetLock)
        {
            if (_targetLookupTask is null || !_targetLookupTask.IsCompleted)
            {
                return;
            }

            result = _targetLookupTask.GetAwaiter().GetResult();
            completedLookupCts = _targetLookupCts;
            _targetLookupTask = null;
            _targetLookupCts = null;
        }

        completedLookupCts?.Dispose();

        var applied = false;
        lock (_targetLock)
        {
            if (result.Generation != _targetGeneration
                || result.ShardType != _currentTargetShard
                || !string.Equals(result.TargetName, _currentTargetName, StringComparison.OrdinalIgnoreCase)
                || _currentTargetMembership == TargetMembership.NonMember)
            {
                return;
            }

            if (result.Canceled)
            {
                return;
            }

            if (_lastLookupStartedAtUtc != DateTimeOffset.MinValue)
            {
                var lookupDuration = nowUtc - _lastLookupStartedAtUtc;
                _diagnostics?.Log($"[Timing] target lookup: {lookupDuration.TotalMilliseconds:F0} ms");
            }

            _lastLookupAtUtc = nowUtc;
            if (result.Profile is null)
            {
                _nextTargetLookupAtUtc = nowUtc.Add(FailedLookupRetryDelay);
            }
            else if (HasPlayerData(result.Profile))
            {
                var profile = result.Profile!;
                if (!string.Equals(profile.Name, result.TargetName, StringComparison.OrdinalIgnoreCase))
                {
                    profile = profile with { Name = result.TargetName };
                }

                _lastTarget = profile;
                _currentTargetMembership = TargetMembership.Member;
                _nextTargetLookupAtUtc = DateTimeOffset.MaxValue;
                profileToCache = profile;
                applied = true;
            }
            else
            {
                _nextTargetLookupAtUtc = nowUtc.Add(FailedLookupRetryDelay);
            }
        }

        if (result.Error is not null)
        {
            _diagnostics?.Log($"[Target] lookup failed for {result.TargetName}: {result.Error.Message}; retrying later.");
        }
        else if (result.Profile is null)
        {
            _diagnostics?.Log($"[Target] herald returned no profile for {result.TargetName}; keeping last resolved player.");
        }
        else if (!applied)
        {
            _diagnostics?.Log($"[Target] {result.TargetName} is not a player (no player fields); keeping last resolved player.");
        }
        else if (applied)
        {
            var profile = result.Profile;
            var classSource = !string.IsNullOrWhiteSpace(profile.Class) ? "herald" : "none";
            _diagnostics?.Log($"[Target] {result.TargetName} resolved | class={profile.Class ?? "-"} source={classSource} guild={profile.Guild ?? "-"} level={profile.Level?.ToString() ?? "-"} rr={profile.RealmRank ?? "-"}");
        }

        if (profileToCache is not null)
        {
            TrySaveTargetProfile(result.ShardType, profileToCache);
            TryUploadProfileOnline(result.ShardType, profileToCache);
        }
    }

    /// <summary>A lookup result with no player fields is a clicked
    /// mob/NPC/object — never let it overwrite the last resolved player.</summary>
    private static bool HasPlayerData(TargetProfile? profile) =>
        profile is not null &&
        (!string.IsNullOrWhiteSpace(profile.Class)
         || !string.IsNullOrWhiteSpace(profile.Guild)
         || profile.Level is not null
         || !string.IsNullOrWhiteSpace(profile.RealmRank)
         || profile.SoloKills is not null);

    private static bool IsIncomplete(TargetProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.Guild)
            || profile.Level is null
            || string.IsNullOrWhiteSpace(profile.RealmRank);
    }

    private string? GetTargetClass(string targetName)
    {
        lock (_targetLock)
        {
            var displayedTarget = _lastTarget;
            return displayedTarget is not null
                && string.Equals(displayedTarget.Name, targetName, StringComparison.OrdinalIgnoreCase)
                ? displayedTarget.Class
                : null;
        }
    }

    private TargetProfile? GetLastTarget()
    {
        lock (_targetLock)
        {
            return _lastTarget;
        }
    }

    private void OnTargetProfileUpdated(TargetProfile profile)
    {
        ShardType shardType;
        var shouldCache = false;
        lock (_targetLock)
        {
            var updatesDisplayedPlayer = string.Equals(_lastTarget?.Name, profile.Name, StringComparison.OrdinalIgnoreCase);
            var updatesCurrentPlayer = _currentTargetMembership != TargetMembership.NonMember
                && string.Equals(_currentTargetName, profile.Name, StringComparison.OrdinalIgnoreCase);
            if (!updatesDisplayedPlayer && !updatesCurrentPlayer)
            {
                return;
            }

            _lastTarget = profile;
            shardType = _currentTargetShard;
            shouldCache = true;
            _diagnostics?.Log($"[Target] async update {profile.Name} | solo={profile.SoloKills?.ToString() ?? "-"}");
        }

        if (shouldCache)
        {
            TrySaveTargetProfile(shardType, profile);
        }
    }

    private void TrySaveTargetProfile(ShardType shardType, TargetProfile profile)
    {
        try
        {
            _targetProfileCache?.Save(shardType, profile);
        }
        catch (Exception ex)
        {
            _diagnostics?.Log($"[Target] cache write failed for {profile.Name}: {ex.Message}");
        }
    }

    private void TryUploadProfileOnline(ShardType shardType, TargetProfile profile)
    {
        if (_onlineSync is null || _onlineSync.Mode != OnlineSyncMode.ReadWrite)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _onlineSync.TryUploadAsync(shardType, profile).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort upload; diagnostics are handled by the sync service/client.
            }
        });
    }

    public void Dispose()
    {
        foreach (var updates in _profileUpdateSources)
        {
            updates.TargetProfileUpdated -= OnTargetProfileUpdated;
        }

        CancellationTokenSource? lookupCts;
        lock (_targetLock)
        {
            _targetGeneration++;
            lookupCts = _targetLookupCts;
            _targetLookupCts = null;
            _targetLookupTask = null;
        }

        lookupCts?.Cancel();
        lookupCts?.Dispose();
        (_ocrReplaySink as IDisposable)?.Dispose();
    }

    private sealed record TargetLookupResult(
        string TargetName,
        ShardType ShardType,
        long Generation,
        TargetProfile? Profile,
        Exception? Error,
        bool Canceled);

    private sealed class VisibleEventTracker
    {
        private readonly int _missingFramesBeforeReset;
        private readonly Dictionary<string, Observation> _observations = new(StringComparer.OrdinalIgnoreCase);
        private long _frame;

        public VisibleEventTracker(int missingFramesBeforeReset = 2)
        {
            _missingFramesBeforeReset = Math.Max(1, missingFramesBeforeReset);
        }

        public IReadOnlyList<T> ObserveFrame<T>(
            IEnumerable<T> visibleEvents,
            Func<T, string> getKey,
            Func<T, int> getOrdinal)
        {
            _frame++;
            var events = visibleEvents.ToList();
            var previousMaximums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in events)
            {
                var key = getKey(item);
                if (!previousMaximums.ContainsKey(key))
                {
                    previousMaximums[key] = _observations.TryGetValue(key, out var observation)
                        ? observation.MaximumOrdinal
                        : 0;
                }
            }

            var newlyVisible = new List<T>();
            var emittedOccurrences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in events)
            {
                var key = getKey(item);
                var ordinal = Math.Max(1, getOrdinal(item));
                if (ordinal > previousMaximums[key]
                    && emittedOccurrences.Add($"{key}\u001f{ordinal}"))
                {
                    newlyVisible.Add(item);
                }
            }

            foreach (var group in events.GroupBy(getKey, StringComparer.OrdinalIgnoreCase))
            {
                _observations[group.Key] = new Observation(
                    group.Max(item => Math.Max(1, getOrdinal(item))),
                    _frame);
            }

            foreach (var staleKey in _observations
                         .Where(x => _frame - x.Value.LastSeenFrame >= _missingFramesBeforeReset)
                         .Select(x => x.Key)
                         .ToList())
            {
                _observations.Remove(staleKey);
            }

            return newlyVisible;
        }

        private sealed record Observation(int MaximumOrdinal, long LastSeenFrame);
    }
}
