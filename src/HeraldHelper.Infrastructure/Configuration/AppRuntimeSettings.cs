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
    bool OcrReplayEnabled = false)
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

        return new AppRuntimeSettings(
            region, shard, resis, ocr, watchRegions,
            showCastBar, showTarget, showTimers,
            dynamicCastSpeed, estimatedSpellDamage, ocrReplayEnabled);
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

        if (!map.TryGetValue($"daoc.character.{shard.ToString().ToLowerInvariant()}", out var characterName) ||
            string.IsNullOrWhiteSpace(characterName))
        {
            return [];
        }

        var characterKey = NormalizeSettingSegment(characterName);
        var legacyKey = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.{characterName.Trim().ToLowerInvariant()}";
        var key = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.{characterKey}";
        var result = new List<OcrWatchRegion>();
        if (!map.TryGetValue(key, out var raw))
        {
            map.TryGetValue(legacyKey, out raw);
        }
        try
        {
            var items = string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<List<OcrWatchRegion>>(raw) ?? [];
            result.AddRange(items.Where(x => x.Region.Width > 0 && x.Region.Height > 0));
        }
        catch
        {
        }

        if (map.TryGetValue($"daoc.ocr.stats.{shard.ToString().ToLowerInvariant()}.{characterKey}", out var statsRaw) &&
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

    private static string NormalizeSettingSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    }
}
