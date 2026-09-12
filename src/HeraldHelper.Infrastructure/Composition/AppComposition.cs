using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Casting;
using HeraldHelper.Infrastructure.Configuration;
using HeraldHelper.Infrastructure.Herald;
using HeraldHelper.Infrastructure.Ocr;
using HeraldHelper.Infrastructure.Overlay;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Infrastructure.Composition;

public static class AppComposition
{
    public static (GameLoopOrchestrator Orchestrator, DebugOverlayRenderer Overlay, AppRuntimeSettings Settings, ScreenCaptureOcrService Capture) Build(
        IReadOnlyDictionary<string, string> settingsMap,
        IReadOnlyCollection<AbilityDefinition> abilities,
        HttpClient? httpClient = null,
        Func<IReadOnlyDictionary<string, string>>? getSettings = null,
        IShardAuthRefreshService? authRefreshService = null,
        IOverlayRenderer? liveOverlayRenderer = null,
        IResponseDiagnostics? diagnostics = null,
        Func<IReadOnlyCollection<CastSpellOverride>>? getCastSpellOverrides = null,
        Func<CharacterStatsSnapshot?>? loadCharacterStats = null,
        Action<CharacterStatsSnapshot>? saveCharacterStats = null,
        ITargetProfileCache? targetProfileCache = null,
        IOnlineSyncService? onlineSync = null)
    {
        getSettings ??= () => settingsMap;
        var settings = AppRuntimeSettings.FromMap(settingsMap);
        var activeClass = ResolveActiveClass(settingsMap, settings.ShardType);
        var activeLevel = ResolveActiveLevel(settingsMap, settings.ShardType);
        var activeCharacter = ResolveActiveCharacter(settingsMap, settings.ShardType);
        IOcrEngine ocrEngine = settings.OcrEngineMode switch
        {
            OcrEngineMode.Windows => new WindowsBuiltInOcrEngine(),
            OcrEngineMode.Tesseract => new TesseractCliOcrEngine(),
            _ => new AdaptiveOcrEngine(
                new WindowsBuiltInOcrEngine(),
                new TesseractCliOcrEngine())
        };
        var capture = new ScreenCaptureOcrService(ocrEngine);
        IOcrReplaySink? replaySink = settings.OcrReplayEnabled ? new FileOcrReplaySink() : null;
        IChatCaptureService captureService = capture;
        IChatEventParser parser = new AbilitiesChatEventParser(abilities);
        ICastSpellCatalog castSpellCatalog = settings.ShardType switch
        {
            ShardType.Eden => new EdenCastSpellCatalog(),
            ShardType.Blackthorn => new BlackthornCastSpellCatalog(),
            _ => new EmptyCastSpellCatalog()
        };
        if (getCastSpellOverrides is not null && settings.ShardType is ShardType.Eden or ShardType.Blackthorn)
        {
            castSpellCatalog = new OverrideAwareCastSpellCatalog(castSpellCatalog, getCastSpellOverrides);
        }
        var heraldHttpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        IHeraldClientFactory heraldFactory = new HeraldClientFactory(heraldHttpClient, getSettings, authRefreshService, diagnostics);
        ICcImmunityTracker tracker = new CcImmunityTracker();
        var overlay = new DebugOverlayRenderer();
        IOverlayRenderer finalOverlay = liveOverlayRenderer is null
            ? overlay
            : new CompositeOverlayRenderer([overlay, liveOverlayRenderer]);

        var orchestrator = new GameLoopOrchestrator(
            captureService,
            parser,
            castSpellCatalog,
            heraldFactory,
            tracker,
            finalOverlay,
            settings.OcrWatchRegions,
            diagnostics,
            activeClass,
            activeLevel,
            activeCharacter,
            loadCharacterStats,
            saveCharacterStats,
            settings.DynamicCastSpeed,
            settings.EstimatedSpellDamage,
            replaySink,
            targetProfileCache,
            onlineSync);

        return (orchestrator, overlay, settings, capture);
    }

    private static string? ResolveActiveClass(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        settings.TryGetValue(CharacterSettingsKeys.SelectedCharacter(shard), out var character);
        if (settings.TryGetValue(CharacterSettingsKeys.AbilityProfileClass(shard, character ?? string.Empty), out var selectedClass) &&
            !string.IsNullOrWhiteSpace(selectedClass))
        {
            return selectedClass.Trim();
        }
        return settings.TryGetValue(CharacterSettingsKeys.LegacyAbilityProfileClass(shard), out var legacyClass) &&
               !string.IsNullOrWhiteSpace(legacyClass)
            ? legacyClass.Trim()
            : null;
    }

    private static string ResolveActiveCharacter(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        return settings.TryGetValue(CharacterSettingsKeys.SelectedCharacter(shard), out var character)
            ? character.Trim()
            : string.Empty;
    }

    private static int? ResolveActiveLevel(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        settings.TryGetValue(CharacterSettingsKeys.SelectedCharacter(shard), out var character);
        return settings.TryGetValue(CharacterSettingsKeys.CharacterLevel(shard, character ?? string.Empty), out var raw) &&
               int.TryParse(raw, out var level) && level > 0
            ? level
            : null;
    }
}
