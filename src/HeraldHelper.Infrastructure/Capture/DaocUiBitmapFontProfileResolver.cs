using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

internal sealed record DaocUiValueField(
    string Label,
    string Adapter,
    int X,
    int Y,
    int Width,
    int Height);

internal sealed record DaocUiBitmapFontProfile(
    string WindowPath,
    string FontName,
    string FontPath,
    IReadOnlyList<DaocUiValueField> Fields);

internal static class DaocUiBitmapFontProfileResolver
{
    private static readonly Regex CustomWindowRegex = new(
        @"^custom(?<number>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BlackthornGamePathRegex = new(
        @"game_path=(?<path>[A-Za-z]:\\[^\r\n]+?\.dll)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DaocUiBitmapFontProfile? Resolve(OcrWatchRegion watchRegion, ShardType shardType)
    {
        var match = CustomWindowRegex.Match(watchRegion.Key.Trim());
        if (!match.Success)
        {
            return null;
        }

        var gameRoot = FindGameRoot(shardType);
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        return ResolveFromGameRoot(watchRegion, gameRoot);
    }

    internal static DaocUiBitmapFontProfile? ResolveFromGameRoot(OcrWatchRegion watchRegion, string gameRoot)
    {
        var match = CustomWindowRegex.Match(watchRegion.Key.Trim());
        if (!match.Success)
        {
            return null;
        }

        var windowName = $"custom{match.Groups["number"].Value}_window";
        var windowPath = FindWindowPath(gameRoot, windowName);
        if (windowPath is null)
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(windowPath, LoadOptions.None);
            var template = document.Descendants("WindowTemplate")
                .FirstOrDefault(element => string.Equals(
                    element.Element("Name")?.Value.Trim(),
                    windowName,
                    StringComparison.OrdinalIgnoreCase));
            if (template is null)
            {
                return null;
            }

            var controls = template.Elements()
                .Where(element => !string.IsNullOrWhiteSpace(element.Element("ControlId")?.Value))
                .ToArray();
            var controlsById = controls
                .GroupBy(
                    element => element.Element("ControlId")!.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            var fields = new List<(DaocUiValueField Field, string FontName)>();
            foreach (var control in controls)
            {
                var adapter = control.Element("Adapter")?.Value.Trim();
                var fontName = control.Element("FontName")?.Value.Trim();
                var controlId = control.Element("ControlId")?.Value.Trim();
                var position = control.Element("Position");
                if (string.IsNullOrWhiteSpace(adapter) ||
                    string.IsNullOrWhiteSpace(fontName) ||
                    string.IsNullOrWhiteSpace(controlId) ||
                    position is null ||
                    !TryReadPositiveOrZero(position.Element("X"), out var x) ||
                    !TryReadPositiveOrZero(position.Element("Y"), out var y))
                {
                    continue;
                }

                var labelId = controlId.EndsWith("_data", StringComparison.OrdinalIgnoreCase)
                    ? controlId[..^5]
                    : controlId;
                var label = controlsById.TryGetValue(labelId, out var labelControl)
                    ? labelControl.Element("Data")?.Value.Trim()
                    : null;
                label = string.IsNullOrWhiteSpace(label) ? LabelFromAdapter(adapter) : label;

                var width = TryReadPositive(control.Element("Width"), out var parsedWidth)
                    ? parsedWidth
                    : watchRegion.Region.Width - x;
                var height = TryReadPositive(control.Element("Height"), out var parsedHeight)
                    ? parsedHeight
                    : 16;
                fields.Add((new DaocUiValueField(label, adapter, x, y, width, height), fontName));
            }

            if (fields.Count < 2)
            {
                return null;
            }

            var selectedFont = fields
                .GroupBy(item => item.FontName, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .First();
            var orderedFields = fields
                .Where(item => string.Equals(item.FontName, selectedFont, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Field)
                .OrderBy(field => field.Y)
                .ToArray();
            var selectedFields = orderedFields
                .Select((field, index) => field with
                {
                    Height = Math.Min(field.Height, ResolveRowHeight(orderedFields, index))
                })
                .ToArray();
            var fontPath = FindFontPath(gameRoot, selectedFont);
            return fontPath is null
                ? null
                : new DaocUiBitmapFontProfile(windowPath, selectedFont, fontPath, selectedFields);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindWindowPath(string gameRoot, string windowName)
    {
        foreach (var path in new[]
        {
            Path.Combine(gameRoot, "ui", "customs", $"{windowName}.xml"),
            Path.Combine(gameRoot, "ui", "custom", $"{windowName}.xml")
        })
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    private static string? FindFontPath(string gameRoot, string fontName)
    {
        var catalogPaths = new[]
        {
            Path.Combine(gameRoot, "ui", "custom", "runtime", "catalogs", "fonts.xml"),
            Path.Combine(gameRoot, "ui", "customs", "fonts.xml"),
            Path.Combine(gameRoot, "ui", "custom", "fonts.xml")
        };

        foreach (var catalogPath in catalogPaths.Where(File.Exists))
        {
            try
            {
                var document = XDocument.Load(catalogPath, LoadOptions.None);
                var file = document.Descendants("Font")
                    .FirstOrDefault(element => string.Equals(
                        element.Element("Name")?.Value.Trim(),
                        fontName,
                        StringComparison.OrdinalIgnoreCase))
                    ?.Element("File")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(file) || !file.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalized = file.Replace('/', Path.DirectorySeparatorChar);
                var path = Path.GetFullPath(Path.Combine(gameRoot, "ui", normalized));
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch
            {
            }
        }
        return null;
    }

    internal static string? FindGameRoot(ShardType shardType) => shardType switch
    {
        ShardType.Eden => FindEdenGameRoot(),
        ShardType.Blackthorn => FindBlackthornGameRoot(),
        _ => null
    };

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
                var match = BlackthornGamePathRegex.Match(line);
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

    private static string LabelFromAdapter(string adapter)
    {
        var label = adapter.StartsWith("stats_", StringComparison.OrdinalIgnoreCase)
            ? adapter[6..]
            : adapter;
        return $"{label.Replace('_', ' ')}:";
    }

    private static int ResolveRowHeight(IReadOnlyList<DaocUiValueField> fields, int index)
    {
        if (index + 1 < fields.Count)
        {
            return Math.Max(1, fields[index + 1].Y - fields[index].Y);
        }
        if (index > 0)
        {
            return Math.Max(1, fields[index].Y - fields[index - 1].Y);
        }
        return fields[index].Height;
    }

    private static bool TryReadPositiveOrZero(XElement? element, out int value) =>
        int.TryParse(element?.Value, out value) && value >= 0;

    private static bool TryReadPositive(XElement? element, out int value) =>
        int.TryParse(element?.Value, out value) && value > 0;
}
