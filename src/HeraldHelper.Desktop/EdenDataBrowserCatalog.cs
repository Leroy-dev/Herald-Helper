using System.IO;
using System.Text;
using System.Text.Json;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal static class EdenDataBrowserCatalog
{
    public static IReadOnlyList<CatalogBrowserEntry> Load(IReadOnlyDictionary<string, CatalogEntryOverride>? overrides = null)
    {
        return LoadEntries(overrides ?? new Dictionary<string, CatalogEntryOverride>(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<CatalogBrowserEntry> LoadEntries(IReadOnlyDictionary<string, CatalogEntryOverride> overrides)
    {
        var dataRoot = TryFindDataRoot();
        if (dataRoot is null)
        {
            return [];
        }

        var classesDir = Path.Combine(dataRoot, "generated", "classes");
        if (!Directory.Exists(classesDir))
        {
            return [];
        }

        var entries = new List<CatalogBrowserEntry>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(classesDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(path), "index.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var className = root.TryGetProperty("name", out var classNameEl) ? classNameEl.GetString() ?? "Unknown" : "Unknown";

            if (root.TryGetProperty("realmAbilities", out var realmAbilities) && realmAbilities.ValueKind == JsonValueKind.Array)
            {
                foreach (var realmAbility in realmAbilities.EnumerateArray())
                {
                    var entry = TryCreateRealmAbilityEntry(className, realmAbility);
                    if (entry is not null && dedupe.Add(BuildKey(entry)))
                    {
                        entries.Add(ApplyOverride(entry, overrides));
                    }
                }
            }

            CollectSkillEntries(root, className, entries, dedupe, overrides);
        }

        return entries
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Level ?? int.MaxValue)
            .ToList();
    }

    private static void CollectSkillEntries(
        JsonElement element,
        string className,
        List<CatalogBrowserEntry> entries,
        HashSet<string> dedupe,
        IReadOnlyDictionary<string, CatalogEntryOverride> overrides)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var entry = TryCreateSkillEntry(className, element);
                if (entry is not null && dedupe.Add(BuildKey(entry)))
                {
                    entries.Add(ApplyOverride(entry, overrides));
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectSkillEntries(property.Value, className, entries, dedupe, overrides);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectSkillEntries(item, className, entries, dedupe, overrides);
                }
                break;
        }
    }

    private static CatalogBrowserEntry? TryCreateRealmAbilityEntry(string className, JsonElement element)
    {
        if (!TryReadString(element, "name", out var name))
        {
            return null;
        }

        var description = TryReadString(element, "description", out var descRaw) ? descRaw : string.Empty;
        var details = new StringBuilder();
        details.AppendLine(name);
        details.AppendLine($"Type: Realm Ability");
        details.AppendLine($"Class: {className}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            details.AppendLine();
            details.AppendLine(description.Trim());
        }

        return new CatalogBrowserEntry(
            BuildEntryKey(className, "Realm Ability", name.Trim(), null, "Realm Abilities"),
            name.Trim(),
            "Realm Ability",
            className,
            "Realm Abilities",
            null,
            ParseCastTimeFromDescription(description),
            BuildSummary(description),
            details.ToString().Trim(),
            ReadIconRef(element));
    }

    private static CatalogBrowserEntry? TryCreateSkillEntry(string className, JsonElement element)
    {
        if (!TryReadString(element, "name", out var name) ||
            !element.TryGetProperty("attributes", out var attributes) ||
            attributes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var level = element.TryGetProperty("level", out var levelEl) && levelEl.TryGetInt32(out var parsedLevel)
            ? parsedLevel
            : (int?)null;
        var castTime = ParseCastTimeFromAttributes(attributes);
        var category = TryGetCategory(element);
        var details = BuildSkillDetails(className, name, category, level, attributes);

        return new CatalogBrowserEntry(
            BuildEntryKey(className, "Spell / Ability", name.Trim(), level, category),
            name.Trim(),
            "Spell / Ability",
            className,
            category,
            level,
            castTime,
            BuildSummaryFromAttributes(attributes),
            details,
            ReadIconRef(element));
    }

    private static string BuildSkillDetails(string className, string name, string category, int? level, JsonElement attributes)
    {
        var builder = new StringBuilder();
        builder.AppendLine(name.Trim());
        builder.AppendLine("Type: Spell / Ability");
        builder.AppendLine($"Class: {className}");
        if (!string.IsNullOrWhiteSpace(category))
        {
            builder.AppendLine($"Category: {category}");
        }

        if (level is not null)
        {
            builder.AppendLine($"Level: {level.Value}");
        }

        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!TryReadString(attribute, "name", out var attrName) || !TryReadString(attribute, "value", out var attrValue))
            {
                continue;
            }

            builder.AppendLine($"{attrName}: {attrValue}");
        }

        return builder.ToString().Trim();
    }

    private static IconSpriteRef? ReadIconRef(JsonElement element)
    {
        if (element.TryGetProperty("icon", out var icon))
        {
            var embedded = ParseIconRef(icon);
            if (embedded is not null)
            {
                return embedded;
            }
        }

        return element.TryGetProperty("iconId", out var iconIdElement) &&
               iconIdElement.TryGetInt32(out var iconId)
            ? IconCatalog.FindBestForSpell(iconId)
            : null;
    }

    private static IconSpriteRef? ParseIconRef(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryReadString(element, "spriteSheet", out var spriteSheet) ||
            !element.TryGetProperty("sprite", out var sprite))
        {
            return null;
        }

        return new IconSpriteRef(
            spriteSheet,
            ReadInt(sprite, "x"),
            ReadInt(sprite, "y"),
            ReadInt(sprite, "width"),
            ReadInt(sprite, "height"),
            ReadNestedInt(element, "overlays", "border"),
            ReadNestedInt(element, "overlays", "spellBadge"),
            ReadCornerIndex(element, "upLeft"),
            ReadCornerIndex(element, "up"),
            ReadCornerIndex(element, "upRight"),
            ReadCornerIndex(element, "right"),
            ReadCornerIndex(element, "downRight"),
            ReadCornerIndex(element, "down"),
            ReadCornerIndex(element, "left"));
    }

    private static double? ParseCastTimeFromDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        const string marker = "Casting Time:";
        var index = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var segment = description[index..];
        var lineEnd = segment.IndexOfAny(['\r', '\n']);
        if (lineEnd >= 0)
        {
            segment = segment[..lineEnd];
        }

        if (segment.Contains("Instant", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var parts = segment.Split(':', 2);
        return parts.Length == 2 ? ParseDurationSeconds(parts[1]) : null;
    }

    private static double? ParseCastTimeFromAttributes(JsonElement attributes)
    {
        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!TryReadString(attribute, "name", out var attrName) ||
                !string.Equals(attrName, "Cast Time", StringComparison.OrdinalIgnoreCase) ||
                !TryReadString(attribute, "value", out var attrValue))
            {
                continue;
            }

            return ParseDurationSeconds(attrValue);
        }

        return null;
    }

    private static double? ParseDurationSeconds(string raw)
    {
        var normalized = raw
            .Replace("seconds", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("second", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("secs", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("sec", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("s", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string BuildSummary(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var firstLine = description
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return firstLine.Trim();
    }

    private static string BuildSummaryFromAttributes(JsonElement attributes)
    {
        var parts = new List<string>();
        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!TryReadString(attribute, "name", out var attrName) || !TryReadString(attribute, "value", out var attrValue))
            {
                continue;
            }

            if (string.Equals(attrName, "Type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attrName, "Target", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attrName, "Range", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attrName, "Cast Time", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"{attrName}: {attrValue}");
            }
        }

        return string.Join(" | ", parts);
    }

    private static string TryGetCategory(JsonElement element)
    {
        if (!element.TryGetProperty("parentPath", out var parentPath) || parentPath.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var segments = parentPath.EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return segments.Count > 0 ? segments[^1]!.Trim() : string.Empty;
    }

    private static string BuildKey(CatalogBrowserEntry entry)
    {
        return entry.EntryKey;
    }

    private static string BuildEntryKey(string className, string entryType, string name, int? level, string category)
    {
        return $"{className}|{entryType}|{name}|{level?.ToString() ?? "-"}|{category}";
    }

    private static CatalogBrowserEntry ApplyOverride(CatalogBrowserEntry entry, IReadOnlyDictionary<string, CatalogEntryOverride> overrides)
    {
        if (!overrides.TryGetValue(entry.EntryKey, out var value))
        {
            return entry;
        }

        return new CatalogBrowserEntry(
            entry.EntryKey,
            value.Name,
            value.EntryType,
            value.ClassName,
            value.Category,
            value.Level,
            value.CastTimeSeconds,
            value.Summary,
            value.Details,
            value.Icon);
    }

    private static string? TryFindDataRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "eden-charplan");
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
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty(propertyName, out var property))
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
               nested.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
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
