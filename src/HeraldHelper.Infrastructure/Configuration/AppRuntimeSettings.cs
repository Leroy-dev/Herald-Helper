using System.Text.Json;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Configuration;

public sealed record AppRuntimeSettings(
    ScreenRegion? ChatRegion,
    ShardType ShardType,
    int ResistPercent,
    OcrEngineMode OcrEngineMode,
    IReadOnlyList<OcrWatchRegion> OcrWatchRegions,
    bool ShowCastBar = true,
    bool ShowTarget = true,
    bool ShowTimers = true,
    bool DynamicCastSpeed = false,
    bool EstimatedSpellDamage = false,
    bool OcrReplayEnabled = false,
    string? CustomUiFolder = null,
    bool ChatLogCaptureEnabled = false,
    string? ChatLogPath = null,
    IReadOnlyList<string>? ChatLogRegions = null,
    bool ChatLogPumpEnabled = false,
    string? ChatLogPumpKey = null,
    bool BlackthornRelayEnabled = false,
    bool ChatMemReadEnabled = false,
    string? ChatMemProcess = null,
    int? ChatMemRva = null,
    bool StatsMemReadEnabled = false)
{
    public static AppRuntimeSettings LoadFromCfg(string cfgPath)
    {
        if (!File.Exists(cfgPath))
        {
            return new AppRuntimeSettings(null, ShardType.Default, 0, OcrEngineMode.Adaptive, [], true, true, true, false, false, false);
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(cfgPath))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                map[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return FromMap(map);
    }

    public static AppRuntimeSettings FromMap(IReadOnlyDictionary<string, string> map)
    {
        var showCastBar = ReadBool(map, "overlayShowCastBar", true);
        var showTarget = ReadBool(map, "overlayShowTarget", true);
        var showTimers = ReadBool(map, "overlayShowTimers", true);
        var dynamicCastSpeed = ReadBool(map, "dynamicCastSpeedEnabled", false);
        var estimatedSpellDamage = ReadBool(map, "estimatedSpellDamageEnabled", false);
        var ocrReplayEnabled = ReadBool(map, "ocrReplayEnabled", false);
        var region = TryParseRegion(map);
        var shard = map.TryGetValue("server", out var server) ? ParseShard(server) : ShardType.Default;
        var resis = map.TryGetValue("resis", out var resisRaw) && int.TryParse(resisRaw, out var r) ? r : 0;
        resis = Math.Clamp(resis, 0, 60);
        var ocr = map.TryGetValue("ocrEngine", out var ocrRaw) ? ParseOcr(ocrRaw) : OcrEngineMode.Adaptive;
        var watchRegions = ParseOcrWatchRegions(map, shard);
        var customUiFolder = map.TryGetValue("customUiFolder", out var folder) &&
                             !string.IsNullOrWhiteSpace(folder)
            ? folder.Trim()
            : null;
        var chatLogCaptureEnabled = ReadBool(map, "chatLogCaptureEnabled", false);
        var chatLogPath = map.TryGetValue("chatLogPath", out var logPath) &&
                          !string.IsNullOrWhiteSpace(logPath)
            ? logPath.Trim()
            : null;
        var chatLogRegions = map.TryGetValue("chatLogRegions", out var regionsRaw) &&
                             !string.IsNullOrWhiteSpace(regionsRaw)
            ? regionsRaw.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        var chatLogPumpEnabled = ReadBool(map, "chatLogPumpEnabled", false);
        var chatLogPumpKey = map.TryGetValue("chatLogPumpKey", out var pumpKey) &&
                             !string.IsNullOrWhiteSpace(pumpKey)
            ? pumpKey.Trim()
            : null;
        var btRelayEnabled = ReadBool(map, "btRelayEnabled", false);
        var chatMemReadEnabled = ReadBool(map, "chatMemReadEnabled", false);
        var chatMemProcess = map.TryGetValue("chatMemProcess", out var memProc) &&
                             !string.IsNullOrWhiteSpace(memProc)
            ? memProc.Trim()
            : null;
        int? chatMemRva = map.TryGetValue("chatMemRva", out var rvaRaw) &&
                          !string.IsNullOrWhiteSpace(rvaRaw)
            ? (rvaRaw.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(rvaRaw.Trim()[2..], 16)
                : int.Parse(rvaRaw.Trim()))
            : null;

        return new AppRuntimeSettings(
            region, shard, resis, ocr, watchRegions,
            showCastBar, showTarget, showTimers,
            dynamicCastSpeed, estimatedSpellDamage, ocrReplayEnabled, customUiFolder,
            chatLogCaptureEnabled, chatLogPath, chatLogRegions, chatLogPumpEnabled, chatLogPumpKey,
            btRelayEnabled, chatMemReadEnabled, chatMemProcess, chatMemRva,
            // Stats memory read piggybacks on chatMemReadEnabled by default —
            // same process access, same elevation requirement.
            ReadBool(map, "statsMemReadEnabled", chatMemReadEnabled));
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

    private static ScreenRegion? TryParseRegion(IReadOnlyDictionary<string, string> map)
    {
        if (!map.TryGetValue("MX", out var mxRaw) ||
            !map.TryGetValue("MY", out var myRaw) ||
            !map.TryGetValue("w", out var wRaw) ||
            !map.TryGetValue("h", out var hRaw))
        {
            return null;
        }

        if (!int.TryParse(mxRaw, out var xParsed) ||
            !int.TryParse(myRaw, out var yParsed) ||
            !int.TryParse(wRaw, out var wParsed) ||
            !int.TryParse(hRaw, out var hParsed))
        {
            return null;
        }

        if (wParsed <= 0 || hParsed <= 0)
        {
            return null;
        }

        return new ScreenRegion(xParsed, yParsed, wParsed, hParsed);
    }

    private static ShardType ParseShard(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "default" => ShardType.Default,
            "phoenix" => ShardType.Phoenix,
            "titan" => ShardType.Titan,
            "celestius" => ShardType.Celestius,
            "eden" => ShardType.Eden,
            "blackthorn" => ShardType.Blackthorn,
            _ => ShardType.Default
        };
    }

    private static OcrEngineMode ParseOcr(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "windows" => OcrEngineMode.Windows,
            "tesseract" => OcrEngineMode.Tesseract,
            _ => OcrEngineMode.Adaptive
        };
    }

    private static IReadOnlyList<OcrWatchRegion> ParseOcrWatchRegions(IReadOnlyDictionary<string, string> map, ShardType shard)
    {
        if (shard == ShardType.Default)
        {
            return [];
        }

        if (!map.TryGetValue(CharacterSettingsKeys.SelectedCharacter(shard), out var characterName) ||
            string.IsNullOrWhiteSpace(characterName))
        {
            return [];
        }

        var key = CharacterSettingsKeys.OcrWindows(shard, characterName);
        var result = new List<OcrWatchRegion>();
        map.TryGetValue(key, out var raw);
        try
        {
            var items = string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<List<OcrWatchRegion>>(raw) ?? [];
            result.AddRange(items.Where(x => x.Region.Width > 0 && x.Region.Height > 0));
        }
        catch
        {
        }

        if (map.TryGetValue(CharacterSettingsKeys.OcrStats(shard, characterName), out var statsRaw) &&
            !string.IsNullOrWhiteSpace(statsRaw))
        {
            try
            {
                var statsRegion = JsonSerializer.Deserialize<OcrWatchRegion>(statsRaw);
                if (statsRegion is not null && statsRegion.Region.Width > 0 && statsRegion.Region.Height > 0)
                {
                    result.RemoveAll(x => string.Equals(x.Key, "character-stats", StringComparison.OrdinalIgnoreCase));
                    result.Add(statsRegion);
                }
            }
            catch
            {
            }
        }
        return result;
    }
}
