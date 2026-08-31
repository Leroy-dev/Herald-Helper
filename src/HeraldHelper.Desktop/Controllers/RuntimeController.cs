using System.Net.Http;
using System.Text;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Services;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Composition;
using HeraldHelper.Infrastructure.Configuration;
using HeraldHelper.Infrastructure.Overlay;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class RuntimeController
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ICatalogOverrideRepository _catalogOverrideRepository;
    private readonly ICharacterStatsRepository _characterStatsRepository;
    private readonly ITargetProfileCache _targetProfileCache;
    private readonly IAbilityProfileRepository _abilityProfileRepository;
    private readonly IAbilityRepository _abilityRepository;
    private readonly HttpClient _httpClient;
    private readonly IShardAuthRefreshService _authRefreshService;
    private readonly IResponseDiagnostics? _responseDiagnostics;
    private DesktopOverlayRenderer _liveOverlay;

    public RuntimeController(
        ISettingsRepository settingsRepository,
        ICatalogOverrideRepository catalogOverrideRepository,
        ICharacterStatsRepository characterStatsRepository,
        ITargetProfileCache targetProfileCache,
        IAbilityProfileRepository abilityProfileRepository,
        IAbilityRepository abilityRepository,
        HttpClient httpClient,
        IShardAuthRefreshService authRefreshService,
        DesktopOverlayRenderer liveOverlay,
        IResponseDiagnostics? responseDiagnostics = null)
    {
        _settingsRepository = settingsRepository;
        _catalogOverrideRepository = catalogOverrideRepository;
        _characterStatsRepository = characterStatsRepository;
        _targetProfileCache = targetProfileCache;
        _abilityProfileRepository = abilityProfileRepository;
        _abilityRepository = abilityRepository;
        _httpClient = httpClient;
        _authRefreshService = authRefreshService;
        _liveOverlay = liveOverlay;
        _responseDiagnostics = responseDiagnostics;
    }

    public GameLoopOrchestrator? Orchestrator { get; private set; }
    public DebugOverlayRenderer? DebugOverlay { get; private set; }
    public AppRuntimeSettings RuntimeSettings { get; private set; } = new(null, ShardType.Default, 0, OcrEngineMode.Adaptive, [], true, true, true, true);
    public ScreenCaptureOcrService? Capture { get; private set; }

    public async Task<(string Output, OverlaySnapshot? Snapshot, string DiagnosticsText)> TickAsync(
        ScreenRegion? chatRegion,
        ShardType shard,
        int resistPercent,
        CancellationToken cancellationToken)
    {
        await Orchestrator!.TickAsync(
            chatRegion ?? new ScreenRegion(0, 0, 1, 1),
            shard,
            resistPercent,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var output = DebugOverlay!.LastRendered;
        var snapshot = DebugOverlay.LastSnapshot;
        return (output, snapshot, BuildDiagnosticsText(snapshot));
    }

    private string BuildDiagnosticsText(OverlaySnapshot? snapshot)
    {
        var raw = snapshot?.RawOcrText ?? string.Empty;
        if (raw.Length > 700)
        {
            raw = raw[..700] + " ...";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Engine: {Capture!.LastOcrEngineName}");
        sb.AppendLine($"OCR time: {Capture.LastOcrDurationMs} ms");
        sb.AppendLine($"OCR chars: {Capture.LastOcrTextLength}");
        sb.AppendLine($"Target class: {snapshot?.Target?.Class ?? "Unknown"}");
        sb.AppendLine("OCR preview:");
        sb.AppendLine(raw);
        return sb.ToString();
    }

    public void Rebuild(IReadOnlyDictionary<string, string> settingsMap, Action onCharacterStatsSaved)
    {
        Orchestrator?.Dispose();
        var selectedShard = AppRuntimeSettings.FromMap(settingsMap).ShardType;
        var selectedCharacter = ReadOrDefault(
            settingsMap,
            $"daoc.character.{selectedShard.ToString().ToLowerInvariant()}",
            string.Empty);
        var abilities = LoadActiveAbilityDefinitions(settingsMap, selectedShard);
        var getSettings = () => _settingsRepository.LoadSettingsMap();

        (Orchestrator, DebugOverlay, RuntimeSettings, Capture) = AppComposition.Build(
            settingsMap,
            abilities,
            _httpClient,
            getSettings,
            _authRefreshService,
            _liveOverlay,
            _responseDiagnostics,
            () => LoadCastSpellOverrides(selectedShard),
            () => _characterStatsRepository.LoadCharacterStats(selectedShard, selectedCharacter),
            stats =>
            {
                _characterStatsRepository.SaveCharacterStats(stats);
                onCharacterStatsSaved();
            },
            _targetProfileCache);
    }

    private IReadOnlyCollection<CastSpellOverride> LoadCastSpellOverrides(ShardType shard)
    {
        var blackthorn = shard == ShardType.Blackthorn;
        return _catalogOverrideRepository.LoadCatalogEntryOverrides()
            .Values
            .Where(x => x.EntryKey.StartsWith("blackthorn|", StringComparison.OrdinalIgnoreCase) == blackthorn)
            .Select(x => new CastSpellOverride(
                GetOriginalEntryName(x.EntryKey, x.Name),
                x.Name,
                x.CastTimeSeconds,
                x.Icon,
                x.ClassName,
                x.Level))
            .ToList();
    }

    private static string GetOriginalEntryName(string entryKey, string fallbackName)
    {
        var segments = entryKey.Split('|');
        var nameIndex = entryKey.StartsWith("blackthorn|", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
        return segments.Length > nameIndex && !string.IsNullOrWhiteSpace(segments[nameIndex])
            ? segments[nameIndex]
            : fallbackName;
    }

    private List<AbilityDefinition> LoadActiveAbilityDefinitions(
        IReadOnlyDictionary<string, string> settingsMap,
        ShardType shard)
    {
        if (SupportsAbilityProfiles(shard))
        {
            var characterName = ReadOrDefault(
                settingsMap,
                $"daoc.character.{shard.ToString().ToLowerInvariant()}",
                string.Empty);
            var className = ReadOrDefault(
                settingsMap,
                AbilityProfileClassSettingKey(shard, characterName),
                ReadOrDefault(settingsMap, LegacyAbilityProfileClassSettingKey(shard), string.Empty));
            if (!string.IsNullOrWhiteSpace(className))
            {
                return _abilityProfileRepository.LoadAbilityProfile(shard, characterName, className)
                    .Where(x => x.IsEnabled)
                    .Select(ToAbilityDefinition)
                    .ToList();
            }
        }

        return _abilityRepository.LoadAbilities()
            .Where(x => x.IsEnabled)
            .Select(ToAbilityDefinition)
            .ToList();
    }

    private static bool SupportsAbilityProfiles(ShardType shard)
    {
        return shard is ShardType.Eden or ShardType.Blackthorn;
    }

    private static string AbilityProfileClassSettingKey(ShardType shard, string characterName)
    {
        var characterKey = NormalizeSettingSegment(characterName);
        if (string.IsNullOrWhiteSpace(characterKey))
        {
            characterKey = "default";
        }

        return $"ability.profile.class.{shard.ToString().ToLowerInvariant()}.{characterKey}";
    }

    private static string LegacyAbilityProfileClassSettingKey(ShardType shard)
    {
        return $"ability.profile.class.{shard.ToString().ToLowerInvariant()}";
    }

    private static string NormalizeSettingSegment(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static AbilityDefinition ToAbilityDefinition(AbilityEditorRow row)
    {
        return new AbilityDefinition(
            row.AbilityName.Trim(),
            row.SkillCode.Trim().ToLowerInvariant(),
            Math.Max(1, row.DurationSeconds),
            AbilitiesChatEventParser.ParseEffectTypeCode(row.EffectType),
            row.Aliases
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static string ReadOrDefault(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }
}
