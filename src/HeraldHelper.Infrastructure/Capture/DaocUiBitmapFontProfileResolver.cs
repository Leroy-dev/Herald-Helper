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

    public static DaocUiBitmapFontProfile? Resolve(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        string? uiPathOverride = null)
    {
        var match = CustomWindowRegex.Match(watchRegion.Key.Trim());
        if (!match.Success)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(uiPathOverride))
        {
            return ResolveFromCustomPath(watchRegion, uiPathOverride);
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
        var uiRoot = Path.Combine(gameRoot, "ui");
        return ResolveFromPackageDirs(watchRegion, PackageDirsUnder(uiRoot), uiRoot);
    }

    /// <summary>
    /// Resolves against a user-picked path: a game root, its <c>ui</c> folder,
    /// a <c>custom</c>/<c>customs</c> package folder, any folder holding a UI
    /// package directly, or a picked <c>uimain.xml</c> file.
    /// </summary>
    internal static DaocUiBitmapFontProfile? ResolveFromCustomPath(OcrWatchRegion watchRegion, string path)
    {
        var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
        if (File.Exists(fullPath) && fullPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var packageDir = Path.GetDirectoryName(fullPath);
            return packageDir is null
                ? null
                : ResolveFromPackageDirs(watchRegion, [packageDir], Directory.GetParent(packageDir)?.FullName);
        }
        if (!Directory.Exists(fullPath))
        {
            return null;
        }

        if (IsPackageDirName(new DirectoryInfo(fullPath).Name))
        {
            return ResolveFromPackageDirs(watchRegion, [fullPath], Directory.GetParent(fullPath)?.FullName);
        }

        var uiRoot = Directory.Exists(Path.Combine(fullPath, "ui", "custom")) ||
                     Directory.Exists(Path.Combine(fullPath, "ui", "customs"))
            ? Path.Combine(fullPath, "ui")
            : fullPath;
        var packageDirs = PackageDirsUnder(uiRoot);
        return packageDirs.Count > 0
            ? ResolveFromPackageDirs(watchRegion, packageDirs, uiRoot)
            : ResolveFromPackageDirs(watchRegion, [fullPath], Directory.GetParent(fullPath)?.FullName);
    }

    private static DaocUiBitmapFontProfile? ResolveFromPackageDirs(
        OcrWatchRegion watchRegion,
        IReadOnlyList<string> packageDirs,
        string? uiRoot)
    {
        var match = CustomWindowRegex.Match(watchRegion.Key.Trim());
        if (!match.Success)
        {
            return null;
        }

        var windowName = $"custom{match.Groups["number"].Value}_window";
        foreach (var packageDir in packageDirs)
        {
            var document = FindWindowDocument(packageDir, windowName, out var windowPath);
            if (document is null)
            {
                continue;
            }

            var profile = BuildProfile(watchRegion, document, windowName, windowPath!, packageDirs, uiRoot);
            if (profile is not null)
            {
                return profile;
            }
        }
        return null;
    }

    private static DaocUiBitmapFontProfile? BuildProfile(
        OcrWatchRegion watchRegion,
        XDocument document,
        string windowName,
        string windowPath,
        IReadOnlyList<string> packageDirs,
        string? uiRoot)
    {
        try
        {
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
                .Where(element =>
                    !string.IsNullOrWhiteSpace(element.Element("ControlId")?.Value) ||
                    !string.IsNullOrWhiteSpace(element.Element("Adapter")?.Value))
                .ToArray();
            var controlsById = controls
                .Where(element => !string.IsNullOrWhiteSpace(element.Element("ControlId")?.Value))
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
                    position is null ||
                    !TryReadPositiveOrZero(position.Element("X"), out var x) ||
                    !TryReadPositiveOrZero(position.Element("Y"), out var y))
                {
                    continue;
                }

                // Value controls usually pair with a static label via ControlId
                // ("dex_data" -> "dex"), but adapter-bound labels may carry the
                // rendered sample in their own Data ("M:99") and no ControlId.
                string? label = null;
                if (!string.IsNullOrWhiteSpace(controlId))
                {
                    var labelId = controlId.EndsWith("_data", StringComparison.OrdinalIgnoreCase)
                        ? controlId[..^5]
                        : controlId;
                    label = controlsById.TryGetValue(labelId, out var labelControl) &&
                            !ReferenceEquals(labelControl, control)
                        ? labelControl.Element("Data")?.Value.Trim()
                        : null;
                }
                label = string.IsNullOrWhiteSpace(label)
                    ? control.Element("Data")?.Value.Trim()
                    : label;
                label = string.IsNullOrWhiteSpace(label)
                    ? null
                    : label.TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '-', '%');
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
            var fontPath = FindFontPath(packageDirs, uiRoot, selectedFont);
            return fontPath is null
                ? null
                : new DaocUiBitmapFontProfile(windowPath, selectedFont, fontPath, selectedFields);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPackageDirName(string name) =>
        name.Equals("custom", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("customs", StringComparison.OrdinalIgnoreCase);

    private static List<string> PackageDirsUnder(string uiRoot)
    {
        var dirs = new List<string>(2);
        foreach (var name in new[] { "customs", "custom" })
        {
            var path = Path.Combine(uiRoot, name);
            if (Directory.Exists(path))
            {
                dirs.Add(path);
            }
        }
        return dirs;
    }

    private static XDocument? FindWindowDocument(string packageDir, string windowName, out string? windowPath)
    {
        // Flat fast path first — matches the legacy layout — then a recursive
        // scan so per-window feature folders (and stub <Include> targets, which
        // live under the package dir) are covered the same way.
        var candidates = new List<string>();
        var flat = Path.Combine(packageDir, $"{windowName}.xml");
        if (File.Exists(flat))
        {
            candidates.Add(flat);
        }
        try
        {
            candidates.AddRange(Directory.EnumerateFiles(packageDir, "*.xml", SearchOption.AllDirectories));
        }
        catch
        {
        }

        foreach (var path in candidates)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }
            if (!text.Contains(windowName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var document = XDocument.Parse(text, LoadOptions.None);
                var hasTemplate = document.Descendants("WindowTemplate")
                    .Any(element => string.Equals(
                        element.Element("Name")?.Value.Trim(),
                        windowName,
                        StringComparison.OrdinalIgnoreCase));
                if (hasTemplate)
                {
                    windowPath = path;
                    return document;
                }
            }
            catch
            {
                // A malformed file that merely mentions the window name must not
                // abort the scan — the real template may live in a later file.
            }
        }

        windowPath = null;
        return null;
    }

    private static string? FindFontPath(
        IReadOnlyList<string> packageDirs,
        string? uiRoot,
        string fontName)
    {
        // Catalogs can live in any package dir — a window found in customs/ can
        // still be backed by custom/runtime/catalogs/fonts.xml — and feature
        // folders may own their own fonts.xml below the package root.
        foreach (var packageDir in packageDirs)
        {
            var catalogPaths = new List<string>
            {
                Path.Combine(packageDir, "runtime", "catalogs", "fonts.xml"),
                Path.Combine(packageDir, "fonts.xml")
            };
            try
            {
                catalogPaths.AddRange(
                    Directory.EnumerateFiles(packageDir, "fonts.xml", SearchOption.AllDirectories)
                        .Where(path => !catalogPaths.Contains(path, StringComparer.OrdinalIgnoreCase)));
            }
            catch
            {
            }

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
                    foreach (var candidate in FontFileBases(normalized, packageDir, uiRoot, catalogPath))
                    {
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                }
            }
        }
        return null;
    }

    private static IEnumerable<string> FontFileBases(
        string normalizedFile,
        string packageDir,
        string? uiRoot,
        string catalogPath)
    {
        // Catalog <File> entries are ui/-relative ("custom/feature/fonts/x.tga").
        if (uiRoot is not null)
        {
            yield return Path.GetFullPath(Path.Combine(uiRoot, normalizedFile));
        }

        // Picked package folders accept the same paths minus the custom/ prefix,
        // plus files written relative to the package dir itself.
        var segments = normalizedFile.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1 && IsPackageDirName(segments[0]))
        {
            yield return Path.GetFullPath(Path.Combine(
                packageDir,
                Path.Combine(segments[1..])));
        }
        yield return Path.GetFullPath(Path.Combine(packageDir, normalizedFile));

        // Some foreign catalogs write paths relative to the catalog file.
        var catalogDir = Path.GetDirectoryName(catalogPath);
        if (catalogDir is not null)
        {
            yield return Path.GetFullPath(Path.Combine(catalogDir, normalizedFile));
        }
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
