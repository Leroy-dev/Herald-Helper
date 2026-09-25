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
    public static (GameLoopOrchestrator Orchestrator, DebugOverlayRenderer Overlay, AppRuntimeSettings Settings, ScreenCaptureOcrService Capture, IWindowAwareChatCaptureService CaptureChain) Build(
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
        IOnlineSyncService? onlineSync = null,
        IAlertSound? alertSound = null)
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
            // Tesseract is optional: bundled tools\tesseract, an install, or a
            // PATH entry. Skip it when absent so evaluation never pays a throw.
            _ => TesseractCliOcrEngine.IsAvailable()
                ? new AdaptiveOcrEngine(new WindowsBuiltInOcrEngine(), new TesseractCliOcrEngine())
                : new AdaptiveOcrEngine(new WindowsBuiltInOcrEngine())
        };
        var capture = new ScreenCaptureOcrService(ocrEngine, settings.CustomUiFolder);
        IOcrReplaySink? replaySink = settings.OcrReplayEnabled ? new FileOcrReplaySink() : null;
        IWindowAwareChatCaptureService windowAwareCapture = capture;
        if (settings.ChatLogCaptureEnabled)
        {
            var pump = settings.ChatLogPumpEnabled
                ? new DaocChatLogPump(DaocChatLogPump.ParseVirtualKeyList(settings.ChatLogPumpKey), pressesPerPoll: 2, diagnostics: diagnostics)
                : null;
            windowAwareCapture = new ChatLogTailCaptureService(capture, settings.ChatLogPath, settings.ChatLogRegions, diagnostics, pump);
        }

        // Blackthorn-only realtime path: the launcher's BTUI relay pushes chat
        // (and other state) over a localhost WebSocket. Wraps whatever capture
        // chain exists for non-chat regions; lazily re-discovers until the
        // session appears on disk.
        if (settings.BlackthornRelayEnabled)
        {
            windowAwareCapture = new BlackthornRelayChatSource(
                windowAwareCapture,
                () => BlackthornRelayDiscovery.TryDiscover(),
                regionKeys: settings.ChatLogRegions,
                diagnostics: diagnostics);
        }

        // Conservative mode suppresses every process-memory source: chat
        // still flows via OCR / chat.log / BT relay, nothing attaches to
        // the game process.
        if (settings.ConservativeMode && (settings.ChatMemReadEnabled || settings.StatsMemReadEnabled))
        {
            diagnostics?.Log("[Capture] conservative mode — memory sources disabled");
        }

        // Highest-precedence source when enabled: read the client's chat.log
        // CRT buffer straight out of process memory. Requires elevation; the
        // FILE* RVA is auto-derived from the module image (chatMemRva overrides).
        if (settings.EffectiveChatMemReadEnabled)
        {
            windowAwareCapture = new DaocMemoryChatSource(
                windowAwareCapture,
                settings.ChatMemProcess,
                settings.ChatMemRva,
                settings.ChatLogRegions,
                diagnostics);

            // Scrollback arena diff is opt-in (scrollbackChatEnabled): it
            // needs no /chatlog, but wraps/merges lines under chat spam.
            // chatMemReadEnabled alone only gets the FILE*-buffer reader.
            if (settings.ScrollbackChatEnabled)
            {
                windowAwareCapture = new DaocScrollbackChatSource(
                    windowAwareCapture,
                    settings.ChatMemProcess,
                    settings.ChatLogRegions,
                    diagnostics);
            }
        }

        // Live stats/adapters from process memory: walks the client's adapter
        // registry map (name -> value record) — resists, stats, HP, group info.
        // Passthrough for chat; merges its IAdapterValueSource over the chain.
        if (settings.EffectiveStatsMemReadEnabled)
        {
            windowAwareCapture = new DaocMemoryStatsSource(
                windowAwareCapture,
                settings.ChatMemProcess,
                diagnostics);
        }
        IChatCaptureService captureService = (IChatCaptureService)windowAwareCapture;
        var layers = new List<string> { "ocr" };
        if (settings.ChatLogCaptureEnabled) layers.Insert(0, "chat.log");
        if (settings.BlackthornRelayEnabled) layers.Insert(0, "relay");
        if (settings.EffectiveChatMemReadEnabled)
        {
            layers.Insert(0, settings.ScrollbackChatEnabled ? "scrollback+chat-mem" : "chat-mem");
        }
        if (settings.EffectiveStatsMemReadEnabled) layers.Insert(0, "stats-mem");
        diagnostics?.Log($"[Capture] chat chain: {string.Join(" -> ", layers)}");
        IChatEventParser parser = new AbilitiesChatEventParser(abilities);
        ICastSpellCatalog castSpellCatalog = settings.ShardType switch
        {
            ShardType.Eden => new EdenCastSpellCatalog(),
            ShardType.Blackthorn => new BlackthornCastSpellCatalog(),
            // Other shards get the server's own Spell table (authoritative
            // for the local OpenDAoC server; empty when the CSV is absent).
            _ => new ServerCastSpellCatalog()
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
            onlineSync,
            windowAwareCapture as IAdapterValueSource,
            alertSound,
            RealmAbilityCooldownTable.Load(settings.ShardType, activeClass));

        return (orchestrator, overlay, settings, capture, windowAwareCapture);
    }

    private static string? ResolveActiveClass(IReadOnlyDictionary<string, string> settings, ShardType shard)
    {
        settings.TryGetValue(CharacterSettingsKeys.SelectedCharacter(shard), out var character);
        if (settings.TryGetValue(CharacterSettingsKeys.AbilityProfileClass(shard, character ?? string.Empty), out var selectedClass) &&
            !string.IsNullOrWhiteSpace(selectedClass))
        {
            return selectedClass.Trim();
        }
        return settings.TryGetValue(CharacterSettingsKeys.DefaultAbilityProfileClass(shard), out var defaultClass) &&
               !string.IsNullOrWhiteSpace(defaultClass)
            ? defaultClass.Trim()
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
