using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal static class BlackthornDataBrowserCatalog
{
    public static IReadOnlyList<CatalogBrowserEntry> Load(IReadOnlyDictionary<string, CatalogEntryOverride>? overrides = null)
    {
        var dataRoot = TryFindDataRoot();
        var classesDir = dataRoot is null ? null : Path.Combine(dataRoot, "generated", "classes");
        if (classesDir is null || !Directory.Exists(classesDir))
        {
            return [];
        }

        overrides ??= new Dictionary<string, CatalogEntryOverride>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<CatalogBrowserEntry>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(classesDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(path), "index.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!TryReadString(root, "name", out var className))
            {
                continue;
            }

            if (root.TryGetProperty("realmAbilities", out var realmAbilities) && realmAbilities.ValueKind == JsonValueKind.Array)
            {
                foreach (var realmAbility in realmAbilities.EnumerateArray())
                {
                    var entry = TryCreateRealmAbility(className, realmAbility);
                    AddEntry(entry, entries, keys, overrides);
                }
            }

            CollectSkills(root, className, entries, keys, overrides);
        }

        return entries
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Level ?? int.MaxValue)
            .ToList();
    }

    private static void CollectSkills(
        JsonElement element,
        string className,
        List<CatalogBrowserEntry> entries,
        HashSet<string> keys,
        IReadOnlyDictionary<string, CatalogEntryOverride> overrides)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AddEntry(TryCreateSkill(className, element), entries, keys, overrides);
                foreach (var property in element.EnumerateObject())
                {
                    CollectSkills(property.Value, className, entries, keys, overrides);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectSkills(item, className, entries, keys, overrides);
                }
                break;
        }
    }

    private static CatalogBrowserEntry? TryCreateSkill(string className, JsonElement element)
    {
        if (!TryReadString(element, "name", out var name) ||
            !TryReadString(element, "objectType", out var entryType) ||
            entryType is not ("Spell" or "Style"))
        {
            return null;
        }

        var category = TryReadString(element, "friendlyName", out var friendlyName)
            ? friendlyName
            : TryReadString(element, "specKeyName", out var specName) ? specName : entryType;
        var description = TryReadString(element, "description", out var descriptionValue)
            ? descriptionValue.Trim()
            : string.Empty;
        var level = ReadNullableInt(element, "level");
        var castTime = ReadPositiveDouble(element, "castTime");
        var details = BuildDetails(name, entryType, className, category, element, description);
        var entryKey = BuildEntryKey(className, entryType, name, level, category);
        return new CatalogBrowserEntry(
            entryKey,
            name.Trim(),
            entryType,
            className,
            category,
            level,
            castTime,
            BuildSummary(element, description),
            details,
            ReadIcon(element, entryType));
    }

    private static CatalogBrowserEntry? TryCreateRealmAbility(string className, JsonElement element)
    {
        if (!TryReadString(element, "name", out var name))
        {
            return null;
        }

        var description = TryReadString(element, "delve", out var delve) ? delve.Trim() : string.Empty;
        return new CatalogBrowserEntry(
            BuildEntryKey(className, "Realm Ability", name, null, "Realm Abilities"),
            name.Trim(),
            "Realm Ability",
            className,
            "Realm Abilities",
            null,
            null,
            description.ReplaceLineEndings(" "),
            $"{name}\nType: Realm Ability\nClass: {className}\n\n{description}".Trim(),
            null);
    }

    private static void AddEntry(
        CatalogBrowserEntry? entry,
        List<CatalogBrowserEntry> entries,
        HashSet<string> keys,
        IReadOnlyDictionary<string, CatalogEntryOverride> overrides)
    {
        if (entry is null || !keys.Add(entry.EntryKey))
        {
            return;
        }

        if (overrides.TryGetValue(entry.EntryKey, out var entryOverride))
        {
            entry = new CatalogBrowserEntry(
                entry.EntryKey,
                entryOverride.Name,
                entryOverride.EntryType,
                entryOverride.ClassName,
                entryOverride.Category,
                entryOverride.Level,
                entryOverride.CastTimeSeconds,
                entryOverride.Summary,
                entryOverride.Details,
                entryOverride.Icon);
        }

        entries.Add(entry);
    }

    private static string BuildDetails(
        string name,
        string entryType,
        string className,
        string category,
        JsonElement element,
        string description)
    {
        var details = new StringBuilder();
        details.AppendLine(name);
        details.AppendLine($"Type: {entryType}");
        details.AppendLine($"Class: {className}");
        details.AppendLine($"Category: {category}");
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name is "name" or "description" or "icon" or "objectType" ||
                property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
            {
                continue;
            }

            details.AppendLine($"{property.Name}: {property.Value.ToString()}");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            details.AppendLine();
            details.AppendLine(description);
        }

        return details.ToString().Trim();
    }

    private static string BuildSummary(JsonElement element, string description)
    {
        var parts = new List<string>();
        var castTime = ReadPositiveDouble(element, "castTime");
        if (castTime is not null)
        {
            parts.Add($"Cast: {castTime.Value.ToString("0.##", CultureInfo.InvariantCulture)}s");
        }
        var duration = ReadPositiveDouble(element, "duration");
        if (duration is not null)
        {
            parts.Add($"Duration: {duration.Value.ToString("0.##", CultureInfo.InvariantCulture)}s");
        }
        if (TryReadString(element, "type", out var type))
        {
            parts.Add($"Type: {type}");
        }
        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.ReplaceLineEndings(" "));
        }
        return string.Join(" | ", parts);
    }

    private static IconSpriteRef? ReadIcon(JsonElement element, string entryType)
    {
        if (!element.TryGetProperty("icon", out var icon) ||
            !icon.TryGetProperty("iconLocation", out var locationProperty) ||
            !locationProperty.TryGetInt32(out var location) || location < 0)
        {
            return null;
        }

        var spriteClass = (location / 100) * 100;
        var sheet = ResolveSpriteSheet(spriteClass, entryType);
        if (sheet is null)
        {
            return null;
        }
        var cell = location % 100;
        return new IconSpriteRef(sheet, cell % 10, cell / 10, 32, 32, 0, 0);
    }

    private static string? ResolveSpriteSheet(int spriteClass, string entryType)
    {
        if (entryType == "Spell" && spriteClass >= 0)
        {
            return $"blackthorn/spells/spl_{spriteClass}.bmp";
        }
        return entryType == "Style" && spriteClass >= 0
            ? $"blackthorn/styles/cbt_{spriteClass}.bmp"
            : null;
    }

    private static string BuildEntryKey(string className, string entryType, string name, int? level, string category)
    {
        return $"blackthorn|{className}|{entryType}|{name}|{level?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}|{category}";
    }

    private static double? ReadPositiveDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out var value) && value > 0
            ? value
            : null;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : null;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryFindDataRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "blackthorn-charplan");
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }
}
