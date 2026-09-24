using System.Text.Json;
using System.Text.Json.Serialization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public sealed class ReplayRunner : IOverlayRenderer, ICcImmunityTracker
{
    private readonly IHeraldClientFactory _heraldClientFactory;
    private readonly ICastSpellCatalog _castSpellCatalog;
    private readonly ReplayFixture _fixture;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly ReplayCaptureService _capture;
    private readonly ReplayEventParser _parser;
    private readonly List<ReplayFrameDiff> _diffs = [];
    private OverlaySnapshot? _lastSnapshot;

    public ReplayRunner(
        IHeraldClientFactory heraldClientFactory,
        ICastSpellCatalog castSpellCatalog,
        ReplayFixture fixture,
        IResponseDiagnostics? diagnostics = null)
    {
        _heraldClientFactory = heraldClientFactory;
        _castSpellCatalog = castSpellCatalog;
        _fixture = fixture;
        _diagnostics = diagnostics;
        _capture = new ReplayCaptureService();
        _parser = new ReplayEventParser();
    }

    public IReadOnlyList<ReplayFrameDiff> Diffs => _diffs;

    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        _diffs.Clear();
        var now = DateTimeOffset.UtcNow;
        var region = new ScreenRegion(0, 0, 800, 200);

        using var orchestrator = new GameLoopOrchestrator(
            _capture,
            _parser,
            _castSpellCatalog,
            _heraldClientFactory,
            this,
            this,
            diagnostics: _diagnostics);

        foreach (var frame in _fixture.Frames)
        {
            _lastSnapshot = null;
            _capture.SetNextText(frame.ChatOcrText);
            _parser.SetNextResult(frame.ParseResult);
            now += frame.Advance ?? TimeSpan.FromMilliseconds(350);

            await orchestrator.TickAsync(region, frame.Shard, frame.ResistPercent, now, cancellationToken);

            var diff = CompareFrame(frame.Index, frame.Expected, _lastSnapshot, now);
            if (!diff.IsEmpty)
            {
                _diffs.Add(diff);
            }
        }

        return _diffs.Count == 0;
    }

    private ReplayFrameDiff CompareFrame(int index, ReplayExpected expected, OverlaySnapshot? actual, DateTimeOffset nowUtc)
    {
        var messages = new List<string>();
        var target = actual?.Target;

        if (expected.TargetName is not null && !string.Equals(expected.TargetName, target?.Name, StringComparison.OrdinalIgnoreCase))
        {
            messages.Add($"target name: expected '{expected.TargetName}', actual '{target?.Name ?? "(null)"}'");
        }

        if (expected.TargetClass is not null && !string.Equals(expected.TargetClass, target?.Class, StringComparison.OrdinalIgnoreCase))
        {
            messages.Add($"target class: expected '{expected.TargetClass}', actual '{target?.Class ?? "(null)"}'");
        }

        if (expected.TargetIsLoading is not null && expected.TargetIsLoading != target?.IsLoading)
        {
            messages.Add($"target loading: expected {expected.TargetIsLoading}, actual {target?.IsLoading}");
        }

        if (expected.ActiveCastSpellName is not null)
        {
            if (actual?.ActiveCast is null)
            {
                messages.Add($"active cast: expected '{expected.ActiveCastSpellName}', actual (null)");
            }
            else if (!string.Equals(expected.ActiveCastSpellName, actual.ActiveCast.SpellName, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add($"active cast: expected '{expected.ActiveCastSpellName}', actual '{actual.ActiveCast.SpellName}'");
            }
        }

        if (expected.ActiveCastRemainingSeconds is not null)
        {
            var actualRemaining = actual?.ActiveCast?.RemainingSeconds(nowUtc) ?? 0;
            if (Math.Abs(expected.ActiveCastRemainingSeconds.Value - actualRemaining) > 0.01)
            {
                messages.Add($"active cast remaining: expected {expected.ActiveCastRemainingSeconds:F3}s, actual {actualRemaining:F3}s");
            }
        }

        if (expected.AbilityHitCount is not null && expected.AbilityHitCount != (actual?.Timers.Count ?? 0))
        {
            messages.Add($"ability hit count: expected {expected.AbilityHitCount}, actual {actual?.Timers.Count ?? 0}");
        }

        return new ReplayFrameDiff(index, messages);
    }

    public Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken)
    {
        _lastSnapshot = snapshot;
        return Task.CompletedTask;
    }

    public void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc)
    {
    }

    public void RetractFreshEntries(IEnumerable<string> targetNames, DateTimeOffset nowUtc, TimeSpan maxAge)
    {
    }

    public IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc)
    {
        return [];
    }

    public static ReplayFixture LoadFixture(string json)
    {
        using var document = JsonDocument.Parse(json);
        var fixture = new ReplayFixture();
        var frames = document.RootElement.GetProperty("frames");

        foreach (var frame in frames.EnumerateArray())
        {
            var chatOcrText = frame.GetProperty("chatOcrText").GetString() ?? string.Empty;
            var parseResult = ReadChatParseResult(frame.GetProperty("parseResult"));
            var expected = ReadExpected(frame.GetProperty("expected"));

            fixture.Frames.Add(new ReplayFrame
            {
                Index = frame.GetProperty("index").GetInt32(),
                ChatOcrText = chatOcrText,
                ParseResult = parseResult,
                Expected = expected,
                Shard = Enum.Parse<ShardType>(frame.GetProperty("shard").GetString() ?? "Eden"),
                ResistPercent = frame.GetProperty("resistPercent").GetInt32(),
                Advance = frame.TryGetProperty("advanceMilliseconds", out var advance)
                    ? TimeSpan.FromMilliseconds(advance.GetDouble())
                    : null
            });
        }

        if (fixture.Frames.Count == 0)
        {
            throw new InvalidDataException("Fixture contained no frames.");
        }

        return fixture;
    }

    private static ChatParseResult ReadChatParseResult(JsonElement element)
    {
        var targetEvent = ReadNullable(element, "targetEvent", ReadTargetEvent);
        var abilityHits = ReadList(element, "abilityHits", ReadAbilityHit);
        var castEvent = ReadNullable(element, "castEvent", ReadCastEvent);
        var visibleTargetEvents = ReadList(element, "visibleTargetEvents", ReadTargetEvent);
        var visibleCastEvents = ReadList(element, "visibleCastEvents", ReadCastEvent);

        if (visibleTargetEvents.Count == 0 && targetEvent is not null)
        {
            visibleTargetEvents = [targetEvent];
        }

        if (visibleCastEvents.Count == 0 && castEvent is not null)
        {
            visibleCastEvents = [castEvent];
        }

        return new ChatParseResult(targetEvent, abilityHits, castEvent, visibleTargetEvents, visibleCastEvents);
    }

    private static T? ReadNullable<T>(JsonElement parent, string propertyName, Func<JsonElement, T> reader)
        where T : class
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return reader(element);
    }

    private static IReadOnlyList<T> ReadList<T>(JsonElement parent, string propertyName, Func<JsonElement, T> reader)
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        var results = new List<T>();
        foreach (var item in element.EnumerateArray())
        {
            results.Add(reader(item));
        }

        return results;
    }

    private static TargetEvent ReadTargetEvent(JsonElement element)
    {
        var name = element.GetProperty("name").GetString() ?? string.Empty;
        var membership = Enum.Parse<TargetMembership>(element.GetProperty("membership").GetString() ?? "Unknown", true);
        var ordinal = 1;
        if (element.TryGetProperty("occurrenceOrdinal", out var ordinalElement))
        {
            ordinal = ordinalElement.GetInt32();
        }

        return new TargetEvent(name, membership, ordinal);
    }

    private static CastEvent ReadCastEvent(JsonElement element)
    {
        var eventType = Enum.Parse<CastEventType>(element.GetProperty("eventType").GetString() ?? "Started", true);
        var spellName = element.GetProperty("spellName").GetString() ?? string.Empty;
        var ordinal = 1;
        if (element.TryGetProperty("occurrenceOrdinal", out var ordinalElement))
        {
            ordinal = ordinalElement.GetInt32();
        }

        return new CastEvent(eventType, spellName, ordinal);
    }

    private static AbilityHit ReadAbilityHit(JsonElement element)
    {
        var targetName = element.GetProperty("targetName").GetString() ?? string.Empty;
        var abilityName = element.GetProperty("abilityName").GetString() ?? string.Empty;
        var skillCode = element.GetProperty("skillCode").GetString() ?? string.Empty;
        var effectType = Enum.Parse<ControlEffectType>(element.GetProperty("effectType").GetString() ?? "Stun", true);
        var baseDuration = element.GetProperty("baseDurationSeconds").GetInt32();
        var landed = element.GetProperty("landedSuccessfully").GetBoolean();
        var ordinal = 1;
        if (element.TryGetProperty("occurrenceOrdinal", out var ordinalElement))
        {
            ordinal = ordinalElement.GetInt32();
        }

        return new AbilityHit(targetName, abilityName, skillCode, effectType, baseDuration, landed, ordinal);
    }

    private static ReplayExpected ReadExpected(JsonElement element)
    {
        var expected = new ReplayExpected();

        if (element.TryGetProperty("targetName", out var targetName) && targetName.ValueKind != JsonValueKind.Null)
        {
            expected.TargetName = targetName.GetString();
        }

        if (element.TryGetProperty("targetClass", out var targetClass) && targetClass.ValueKind != JsonValueKind.Null)
        {
            expected.TargetClass = targetClass.GetString();
        }

        if (element.TryGetProperty("targetIsLoading", out var targetIsLoading) && targetIsLoading.ValueKind != JsonValueKind.Null)
        {
            expected.TargetIsLoading = targetIsLoading.GetBoolean();
        }

        if (element.TryGetProperty("activeCastSpellName", out var activeCastSpellName) && activeCastSpellName.ValueKind != JsonValueKind.Null)
        {
            expected.ActiveCastSpellName = activeCastSpellName.GetString();
        }

        if (element.TryGetProperty("activeCastRemainingSeconds", out var activeCastRemainingSeconds) && activeCastRemainingSeconds.ValueKind != JsonValueKind.Null)
        {
            expected.ActiveCastRemainingSeconds = activeCastRemainingSeconds.GetDouble();
        }

        if (element.TryGetProperty("abilityHitCount", out var abilityHitCount) && abilityHitCount.ValueKind != JsonValueKind.Null)
        {
            expected.AbilityHitCount = abilityHitCount.GetInt32();
        }

        return expected;
    }

    private sealed class ReplayCaptureService : IChatCaptureService
    {
        private string _nextText = string.Empty;

        public void SetNextText(string text)
        {
            _nextText = text;
        }

        public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
        {
            return Task.FromResult(_nextText);
        }
    }

    private sealed class ReplayEventParser : IChatEventParser
    {
        private ChatParseResult _nextResult = new(null, []);

        public void SetNextResult(ChatParseResult result)
        {
            _nextResult = result;
        }

        public ChatParseResult Parse(string ocrText, string? fallbackTargetName = null)
        {
            return _nextResult;
        }
    }
}
