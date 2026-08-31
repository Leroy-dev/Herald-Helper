using System.IO;
using System.Text.Json;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal static class IconCatalog
{
    private static readonly Lazy<IReadOnlyList<IconCatalogEntry>> EntriesLazy = new(LoadEntries);

    public static IReadOnlyList<IconCatalogEntry> Load()
    {
        return EntriesLazy.Value;
    }

    public static IReadOnlyList<IconCatalogEntry> LoadBlackthorn()
    {
        var dataRoot = TryFindDataRoot("blackthorn-charplan");
        var iconRoot = dataRoot is null ? null : Path.Combine(dataRoot, "assets", "icons");
        if (iconRoot is null || !Directory.Exists(iconRoot))
        {
            return [];
        }

        var result = new List<IconCatalogEntry>();
        foreach (var path in Directory.EnumerateFiles(iconRoot, "*.bmp", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var separator = fileName.LastIndexOf('_');
            if (separator < 0 || !int.TryParse(fileName[(separator + 1)..], out var spriteClass))
            {
                continue;
            }

            var mappingType = fileName.StartsWith("spl_", StringComparison.OrdinalIgnoreCase)
                ? "Blackthorn Spell"
                : "Blackthorn Style";
            var relativePath = Path.GetRelativePath(iconRoot, path).Replace('\\', '/');
            for (var cell = 0; cell < 100; cell++)
            {
                var icon = new IconSpriteRef(
                    $"blackthorn/{relativePath}",
                    cell % 10,
                    cell / 10,
                    32,
                    32,
                    0,
                    0);
                result.Add(new IconCatalogEntry(
                    spriteClass + cell,
                    icon.SpriteSheet,
                    spriteClass,
                    mappingType,
                    icon));
            }
        }

        return result.OrderBy(x => x.IconId).ToList();
    }

    public static IconSpriteRef? FindBestForSpell(int iconId)
    {
        return Load()
            .Where(x => x.IconId == iconId)
            .OrderByDescending(x => MappingPriority(x.MappingType))
            .Select(x => x.Icon)
            .FirstOrDefault();
    }

    private static IReadOnlyList<IconCatalogEntry> LoadEntries()
    {
        var dataRoot = TryFindDataRoot("eden-charplan");
        if (dataRoot is null)
        {
            return [];
        }

        var path = Path.Combine(dataRoot, "generated", "icon-configs.json");
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<IconCatalogEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("requestedIconId", out var iconIdElement) || !iconIdElement.TryGetInt32(out var iconId))
            {
                continue;
            }

            if (!item.TryGetProperty("sprite", out var spriteElement) || !TryReadString(item, "spriteSheet", out var spriteSheet))
            {
                continue;
            }

            var icon = new IconSpriteRef(
                spriteSheet,
                ReadInt(spriteElement, "x"),
                ReadInt(spriteElement, "y"),
                ReadInt(spriteElement, "width"),
                ReadInt(spriteElement, "height"),
                ReadNestedInt(item, "overlays", "border"),
                ReadNestedInt(item, "overlays", "spellBadge"),
                ReadCornerIndex(item, "upLeft"),
                ReadCornerIndex(item, "up"),
                ReadCornerIndex(item, "upRight"),
                ReadCornerIndex(item, "right"),
                ReadCornerIndex(item, "downRight"),
                ReadCornerIndex(item, "down"),
                ReadCornerIndex(item, "left"));

            result.Add(new IconCatalogEntry(
                iconId,
                spriteSheet,
                ReadInt(item, "spriteClass"),
                TryReadString(item, "mappingType", out var mappingType) ? mappingType : string.Empty,
                icon));
        }

        return result
            .OrderBy(x => x.IconId)
            .ToList();
    }

    private static string? TryFindDataRoot(string directoryName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", directoryName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static int ReadNestedInt(JsonElement element, string objectName, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return element.TryGetProperty(objectName, out var nested) &&
               nested.ValueKind == JsonValueKind.Object &&
               nested.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static int MappingPriority(string mappingType)
    {
        return mappingType.ToLowerInvariant() switch
        {
            "spell" => 3,
            "direct" => 2,
            "style" => 1,
            _ => 0
        };
    }

    private static int ReadCornerIndex(JsonElement element, string propertyName)
    {
        return element.TryGetProperty("overlays", out var overlays) &&
               overlays.TryGetProperty("corners", out var corners) &&
               corners.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }
}
