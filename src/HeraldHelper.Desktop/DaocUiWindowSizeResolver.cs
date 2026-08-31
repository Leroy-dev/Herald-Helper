using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Desktop;

internal sealed record DaocUiWindowSize(int Width, int Height, string SourcePath);

internal static partial class DaocUiWindowSizeResolver
{
    public static IReadOnlyDictionary<string, DaocUiWindowSize> Load(ShardType shard)
    {
        var gameRoot = shard switch
        {
            ShardType.Eden => FindEdenGameRoot(),
            ShardType.Blackthorn => FindBlackthornGameRoot(),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return new Dictionary<string, DaocUiWindowSize>(StringComparer.OrdinalIgnoreCase);
        }

        return LoadFromUiRoots([
            Path.Combine(gameRoot, "ui", "custom"),
            Path.Combine(gameRoot, "ui", "customs")
        ]);
    }

    internal static IReadOnlyDictionary<string, DaocUiWindowSize> LoadFromUiRoots(IEnumerable<string> uiRoots)
    {
        var result = new Dictionary<string, DaocUiWindowSize>(StringComparer.OrdinalIgnoreCase);
        foreach (var uiRoot in uiRoots.Where(Directory.Exists))
        {
            for (var index = 0; index < 20; index++)
            {
                var key = $"Custom{index}";
                if (result.ContainsKey(key))
                {
                    continue;
                }
                var path = Path.Combine(uiRoot, $"custom{index}_window.xml");
                var size = TryReadWindowSize(path, $"custom{index}_window");
                if (size is not null)
                {
                    result[key] = size;
                }
            }
        }
        return result;
    }

    private static DaocUiWindowSize? TryReadWindowSize(string path, string expectedName)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            var template = document.Descendants("WindowTemplate")
                .FirstOrDefault(x => string.Equals(
                    x.Element("Name")?.Value.Trim(),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase));
            if (template is null ||
                !int.TryParse(template.Element("Width")?.Value, out var width) ||
                !int.TryParse(template.Element("Height")?.Value, out var height) ||
                width <= 0 || height <= 0)
            {
                return null;
            }
            return new DaocUiWindowSize(width, height, path);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindEdenGameRoot()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "eden-launcher",
            "config.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            return document.RootElement.TryGetProperty("gameDir", out var gameDir) &&
                   gameDir.ValueKind == JsonValueKind.String &&
                   Directory.Exists(gameDir.GetString())
                ? gameDir.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindBlackthornGameRoot()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "com.blackthorn",
            "logs",
            "Blackthorn Launcher.log");
        if (!File.Exists(logPath))
        {
            return null;
        }
        try
        {
            foreach (var line in File.ReadLines(logPath).Reverse())
            {
                var match = BlackthornGamePathRegex().Match(line);
                if (!match.Success)
                {
                    continue;
                }
                var directory = Path.GetDirectoryName(match.Groups["path"].Value);
                if (Directory.Exists(directory))
                {
                    return directory;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    [GeneratedRegex(@"game_path=(?<path>[A-Za-z]:\\[^\r\n]+?\.dll)(?:\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BlackthornGamePathRegex();
}
