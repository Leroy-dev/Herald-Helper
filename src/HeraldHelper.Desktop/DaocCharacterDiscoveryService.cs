using System.IO;
using HeraldHelper.Domain.Models;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Desktop;

public sealed class DaocCharacterDiscoveryService
{
    public IReadOnlyDictionary<ShardType, IReadOnlyList<DaocCharacterProfile>> Discover()
    {
        var result = new Dictionary<ShardType, IReadOnlyList<DaocCharacterProfile>>();
        var root = GetRootPath();
        if (!Directory.Exists(root))
        {
            return result;
        }

        foreach (var shard in Enum.GetValues<ShardType>())
        {
            if (shard == ShardType.Default)
            {
                continue;
            }

            var shardDirectory = FindShardDirectory(root, shard);
            if (shardDirectory is null)
            {
                continue;
            }

            var profiles = DiscoverCharacters(shard, shardDirectory);
            if (profiles.Count > 0)
            {
                result[shard] = profiles;
            }
        }

        return result;
    }

    private static string GetRootPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Electronic Arts",
            "Dark Age of Camelot");
    }

    private static string? FindShardDirectory(string root, ShardType shard)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            shard.ToString(),
            shard.ToString().ToLowerInvariant()
        };

        if (shard == ShardType.Eden)
        {
            candidates.Add("eden");
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var directoryName = Path.GetFileName(directory);
            if (candidates.Contains(directoryName))
            {
                return directory;
            }
        }

        return null;
    }

    private static IReadOnlyList<DaocCharacterProfile> DiscoverCharacters(ShardType shard, string shardDirectory)
    {
        var uiWindowSizes = DaocUiWindowSizeResolver.Load(shard);
        var characters = new Dictionary<string, List<IniFileInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var iniFile in Directory.EnumerateFiles(shardDirectory, "*.ini", SearchOption.TopDirectoryOnly))
        {
            var parsed = ParseIniFile(iniFile);
            if (parsed is null)
            {
                continue;
            }

            if (!characters.TryGetValue(parsed.CharacterName, out var files))
            {
                files = [];
                characters[parsed.CharacterName] = files;
            }

            files.Add(parsed);
        }

        var profiles = new List<DaocCharacterProfile>();
        foreach (var entry in characters.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var orderedFiles = entry.Value
                .OrderBy(x => x.ProfileSuffix, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var summary = SummarizeIni(orderedFiles[0].FilePath, uiWindowSizes);
            profiles.Add(new DaocCharacterProfile(
                shard,
                entry.Key,
                shardDirectory,
                orderedFiles[0].FilePath,
                orderedFiles.Select(x => Path.GetFileName(x.FilePath)).ToList(),
                summary.Windows,
                summary.ChatWindowCount,
                summary.QuickbarCount,
                summary.CommandWindowRaw));
        }

        return profiles;
    }

    private static IniFileInfo? ParseIniFile(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return null;
        }

        var dashIndex = stem.LastIndexOf('-');
        var characterName = stem;
        var profileSuffix = stem;
        if (dashIndex > 0 && dashIndex < stem.Length - 1)
        {
            var suffix = stem[(dashIndex + 1)..];
            if (suffix.All(char.IsDigit))
            {
                characterName = stem[..dashIndex];
                profileSuffix = suffix;
            }
        }

        return new IniFileInfo(characterName, profileSuffix, filePath);
    }

    private static IniSummary SummarizeIni(
        string filePath,
        IReadOnlyDictionary<string, DaocUiWindowSize> uiWindowSizes)
    {
        var chatWindowCount = 0;
        var quickbarCount = 0;
        string? commandWindowRaw = null;
        var windows = new List<DaocWindowDefinition>();
        var inPanels = false;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
            {
                inPanels = string.Equals(trimmed, "[Panels]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inPanels)
            {
                continue;
            }

            if (trimmed.StartsWith("[Quickbar", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
            {
                quickbarCount++;
                continue;
            }

            if (!TryParseWindowDefinition(trimmed, uiWindowSizes, out var window))
            {
                if (commandWindowRaw is null && trimmed.StartsWith("CommandWindow=", StringComparison.OrdinalIgnoreCase))
                {
                    commandWindowRaw = trimmed["CommandWindow=".Length..];
                }

                continue;
            }

            windows.Add(window);
            if (window.Key.StartsWith("ChatWindow", StringComparison.OrdinalIgnoreCase))
            {
                chatWindowCount++;
            }
        }

        return new IniSummary(windows, chatWindowCount, quickbarCount, commandWindowRaw);
    }

    internal static bool TryParseWindowDefinition(
        string line,
        IReadOnlyDictionary<string, DaocUiWindowSize> uiWindowSizes,
        out DaocWindowDefinition window)
    {
        window = null!;
        var eqIndex = line.IndexOf('=');
        if (eqIndex <= 0)
        {
            return false;
        }

        var key = line[..eqIndex].Trim();
        var raw = line[(eqIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Exclude internal/system windows that are rarely useful for OCR
        var skipPatterns = new[] { "Invisible", "Screen", "HotBar", "MiniMap", "Buffs", "PotionBelt" };
        if (skipPatterns.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        var numericParts = new List<int>();
        string label = key; // Default label is the key

        if (key.StartsWith("ChatWindow", StringComparison.OrdinalIgnoreCase))
        {
            // ChatWindow format: label,x,y,width,height,... where label is optional and may contain spaces
            label = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : key;
            foreach (var part in parts.Skip(1))
            {
                if (int.TryParse(part, out var value))
                {
                    numericParts.Add(value);
                }
                if (numericParts.Count >= 4)
                {
                    break;
                }
            }

            if (numericParts.Count < 4)
            {
                return false;
            }

            var x = numericParts[0];
            var y = numericParts[1];
            var w = numericParts[2];
            var h = numericParts[3];

            // Validate rectangle dimensions
            if (w <= 0 || h <= 0)
            {
                return false;
            }

            // Clamp to reasonable screen ranges
            if (Math.Abs(x) > 5000 || Math.Abs(y) > 5000)
            {
                return false;
            }

            window = new DaocWindowDefinition(key, label, new ScreenRegion(x, y, w, h));
            return true;
        }

        if (key.StartsWith("Custom", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2 ||
                !int.TryParse(parts[0], out var customX) ||
                !int.TryParse(parts[1], out var customY) ||
                Math.Abs(customX) > 5000 || Math.Abs(customY) > 5000)
            {
                return false;
            }

            if (uiWindowSizes.TryGetValue(key, out var resolvedSize))
            {
                window = new DaocWindowDefinition(
                    key,
                    label,
                    new ScreenRegion(customX, customY, resolvedSize.Width, resolvedSize.Height),
                    SizeIsEstimated: false,
                    resolvedSize.SourcePath);
                return true;
            }

            var fallback = GetCustomWindowFallback(key);
            window = new DaocWindowDefinition(
                key,
                label,
                new ScreenRegion(customX, customY, fallback.Width, fallback.Height),
                SizeIsEstimated: true,
                "Fallback; DAoC INI stores no custom-window size");
            return true;
        }

        // For other window types (Stats, CommandWindow, Compass, etc.)
        // Try the first value as a numeric dimension, then the rest
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var value))
            {
                numericParts.Add(value);
            }

            if (numericParts.Count >= 4)
            {
                break;
            }
        }

        if (numericParts.Count < 4)
        {
            return false;
        }

        var xVal = numericParts[0];
        var yVal = numericParts[1];
        var wVal = numericParts[2];
        var hVal = numericParts[3];

        // Validate rectangle dimensions
        if (wVal <= 0 || hVal <= 0)
        {
            return false;
        }

        // Clamp to reasonable screen ranges
        if (Math.Abs(xVal) > 5000 || Math.Abs(yVal) > 5000)
        {
            return false;
        }

        window = new DaocWindowDefinition(
            key,
            label,
            new ScreenRegion(xVal, yVal, wVal, hVal),
            SizeIsEstimated: true,
            "Heuristic INI interpretation; use Adjust Selected if the preview is wrong");
        return true;
    }

    private static (int Width, int Height) GetCustomWindowFallback(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "custom3" => (63, 123),
            "custom4" => (68, 112),
            _ => (100, 100)
        };
    }

    private sealed record IniFileInfo(string CharacterName, string ProfileSuffix, string FilePath);
    private sealed record IniSummary(IReadOnlyList<DaocWindowDefinition> Windows, int ChatWindowCount, int QuickbarCount, string? CommandWindowRaw);
}
