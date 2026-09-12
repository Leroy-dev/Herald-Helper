using System.Net.Http;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;
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
    private readonly IOnlineSyncService? _onlineSync;
    private readonly IResponseDiagnostics? _responseDiagnostics;
    private readonly DesktopOverlayRenderer _liveOverlay;

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
        IOnlineSyncService? onlineSync = null,
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
        _onlineSync = onlineSync;
        _liveOverlay = liveOverlay;
        _responseDiagnostics = responseDiagnostics;
    }

    public RuntimeSession Rebuild(IReadOnlyDictionary<string, string> settingsMap, Action onCharacterStatsSaved)
    {
        var selectedShard = AppRuntimeSettings.FromMap(settingsMap).ShardType;
        var selectedCharacter = ReadOrDefault(
            settingsMap,
            CharacterSettingsKeys.SelectedCharacter(selectedShard),
            string.Empty);
        var abilities = LoadActiveAbilityDefinitions(settingsMap, selectedShard);
        var getSettings = () => _settingsRepository.LoadSettingsMap();

        var (orchestrator, debugOverlay, runtimeSettings, capture) = AppComposition.Build(
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
            _targetProfileCache,
            _onlineSync);

        return new RuntimeSession(orchestrator, debugOverlay, runtimeSettings, capture);
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
                CharacterSettingsKeys.SelectedCharacter(shard),
                string.Empty);
            var className = ReadOrDefault(
                settingsMap,
                CharacterSettingsKeys.AbilityProfileClass(shard, characterName),
                ReadOrDefault(settingsMap, CharacterSettingsKeys.LegacyAbilityProfileClass(shard), string.Empty));
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
