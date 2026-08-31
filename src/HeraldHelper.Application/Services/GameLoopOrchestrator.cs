using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
        ITargetProfileCache? targetProfileCache = null)
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
        var tickStopwatch = Stopwatch.StartNew();
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

        var ocrText = string.Join(Environment.NewLine + Environment.NewLine, ocrSegments);
        ObserveCharacterStats(ocrText, shardType, nowUtc);
        var parseStopwatch = Stopwatch.StartNew();
        var parseResult = _chatEventParser.Parse(ocrText);
        parseStopwatch.Stop();
        _diagnostics?.Log($"[Timing] parse: {parseStopwatch.ElapsedMilliseconds} ms");

        var replayStopwatch = Stopwatch.StartNew();
        _ocrReplaySink?.Record(shardType, _activeCharacterName, nowUtc, replayCaptures, parseResult);
        replayStopwatch.Stop();
        _diagnostics?.Log($"[Timing] replay write: {replayStopwatch.ElapsedMilliseconds} ms");
        var visibleCastEvents = parseResult.VisibleCastEvents
            ?? (parseResult.CastEvent is null ? [] : [parseResult.CastEvent]);
        var newCastEvents = _castEventTracker.ObserveFrame(
            visibleCastEvents,
            BuildCastEventKey,
            static x => x.OccurrenceOrdinal);
        UpdateActiveCast(newCastEvents.LastOrDefault(), nowUtc);
        ApplyCompletedTargetLookup(nowUtc);

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

        if (targetEvent is not null)
        {
            HandleTargetEvent(targetEvent, shardType, nowUtc, cancellationToken);
            ApplyCompletedTargetLookup(nowUtc);
        }

        var newAbilityHits = _abilityEventTracker.ObserveFrame(
            parseResult.AbilityHits,
            BuildAbilityEventKey,
            static x => x.OccurrenceOrdinal);
        foreach (var hit in newAbilityHits)
        {
            _ccImmunityTracker.RegisterSuccessfulHit(hit, GetTargetClass(hit.TargetName), resistPercent, nowUtc);
        }

        var timers = _ccImmunityTracker.GetActiveTimers(nowUtc);
        if (_activeCast is not null && !_activeCast.IsActive(nowUtc))
        {
            _activeCast = null;
        }

        var renderStopwatch = Stopwatch.StartNew();
        await _overlayRenderer.RenderAsync(new OverlaySnapshot(GetLastTarget(), timers, _activeCast, ocrText), cancellationToken);
        renderStopwatch.Stop();
        _diagnostics?.Log($"[Timing] render: {renderStopwatch.ElapsedMilliseconds} ms");

        tickStopwatch.Stop();
        _diagnostics?.Log($"[Timing] tick: {tickStopwatch.ElapsedMilliseconds} ms");
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
            targetName,
            shardType,
            _targetGeneration,
            _targetLookupCts.Token);
        return true;
    }

    private static async Task<TargetLookupResult> LookupTargetAsync(
        IHeraldClient client,
        string targetName,
        ShardType shardType,
        long generation,
        CancellationToken cancellationToken)
    {
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
            else
            {
                _lastTarget = result.Profile;
                _currentTargetMembership = TargetMembership.Member;
                _nextTargetLookupAtUtc = DateTimeOffset.MaxValue;
                profileToCache = result.Profile;
                applied = true;
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
        else if (applied)
        {
            var profile = result.Profile;
            var classSource = !string.IsNullOrWhiteSpace(profile.Class) ? "herald" : "none";
            _diagnostics?.Log($"[Target] {result.TargetName} resolved | class={profile.Class ?? "-"} source={classSource} guild={profile.Guild ?? "-"} level={profile.Level?.ToString() ?? "-"} rr={profile.RealmRank ?? "-"}");
        }

        if (profileToCache is not null)
        {
            TrySaveTargetProfile(result.ShardType, profileToCache);
        }
    }

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
