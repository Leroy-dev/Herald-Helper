using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

public sealed class EdenCastSpellCatalog : ICastSpellCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> _byName;

    public EdenCastSpellCatalog()
    {
        _byName = LoadCatalog();
    }

    public CastSpellInfo? FindBySpellName(string spellName)
    {
        return FindBySpellName(spellName, null, null);
    }

    public CastSpellInfo? FindBySpellName(string spellName, string? className, int? level)
    {
        if (string.IsNullOrWhiteSpace(spellName))
        {
            return null;
        }

        _byName.TryGetValue(NormalizeName(spellName), out var candidates);
        var info = SelectBest(candidates, className, level);
        if (info is not null)
        {
            return info;
        }

        _byName.TryGetValue(NormalizeName(StripGenericCastWords(spellName)), out candidates);
        info = SelectBest(candidates, className, level);
        if (info is not null)
        {
            return info;
        }
        if (string.IsNullOrWhiteSpace(className))
        {
            return null;
        }
        var classKeys = _byName.Where(x => x.Value.Any(y => string.Equals(y.ClassName, className, StringComparison.OrdinalIgnoreCase))).Select(x => x.Key);
        var fuzzyKey = CastNameMatcher.FindClosest(classKeys, StripGenericCastWords(spellName));
        return fuzzyKey is not null && _byName.TryGetValue(fuzzyKey, out candidates)
            ? SelectBest(candidates, className, level)
            : null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> LoadCatalog()
    {
        var dataRoot = TryFindDataRoot();
        if (dataRoot is null)
        {
            return new Dictionary<string, IReadOnlyList<CastSpellInfo>>(StringComparer.OrdinalIgnoreCase);
        }

        var iconIndex = LoadIconIndex(Path.Combine(dataRoot, "generated", "icon-configs.json"));
        var classesDir = Path.Combine(dataRoot, "generated", "classes");
        if (!Directory.Exists(classesDir))
        {
            return new Dictionary<string, IReadOnlyList<CastSpellInfo>>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, List<CastSpellInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(classesDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(path), "index.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (TryReadString(document.RootElement, "name", out var className))
            {
                CollectEntries(document.RootElement, className, result, iconIndex);
            }
        }

        return result.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<CastSpellInfo>)x.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<int, IndexedIcon> LoadIconIndex(string path)
    {
        var icons = new Dictionary<int, IndexedIcon>();
        if (!File.Exists(path))
        {
            return icons;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("requestedIconId", out var idElement) || !idElement.TryGetInt32(out var iconId))
            {
                continue;
            }

            if (!item.TryGetProperty("spriteSheet", out var sheetElement) || !item.TryGetProperty("sprite", out var spriteElement))
            {
                continue;
            }

            var spriteSheet = sheetElement.GetString();
            if (string.IsNullOrWhiteSpace(spriteSheet))
            {
                continue;
            }

            var candidate = new IconSpriteRef(
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
            var mappingType = item.TryGetProperty("mappingType", out var mappingTypeElement)
                ? mappingTypeElement.GetString() ?? string.Empty
                : string.Empty;

            if (!icons.TryGetValue(iconId, out var existing)
                || MappingPriority(mappingType) > MappingPriority(existing.MappingType))
            {
                icons[iconId] = new IndexedIcon(candidate, mappingType);
            }
        }

        return icons;
    }

    private static void CollectEntries(
        JsonElement element,
        string className,
        Dictionary<string, List<CastSpellInfo>> result,
        IReadOnlyDictionary<int, IndexedIcon> iconIndex)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TryAddSpellEntry(element, className, result, iconIndex);
                foreach (var property in element.EnumerateObject())
                {
                    CollectEntries(property.Value, className, result, iconIndex);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectEntries(item, className, result, iconIndex);
                }
                break;
        }
    }

    private static void TryAddSpellEntry(
        JsonElement element,
        string className,
        Dictionary<string, List<CastSpellInfo>> result,
        IReadOnlyDictionary<int, IndexedIcon> iconIndex)
    {
        if (!TryReadString(element, "name", out var name))
        {
            return;
        }

        double? castTimeSeconds = null;
        if (TryReadString(element, "description", out var description))
        {
            castTimeSeconds = ParseCastTimeSeconds(description);
        }

        castTimeSeconds ??= ParseCastTimeSecondsFromAttributes(element);
        if (castTimeSeconds is null || castTimeSeconds <= 0)
        {
            return;
        }

        IconSpriteRef? icon = null;
        if (element.TryGetProperty("icon", out var iconElement))
        {
            icon = ParseIconRef(iconElement);
        }

        if (icon is null && element.TryGetProperty("iconId", out var iconIdElement) && iconIdElement.TryGetInt32(out var iconId))
        {
            if (iconIndex.TryGetValue(iconId, out var indexedIcon))
            {
                icon = indexedIcon.Icon;
            }
        }

        var candidate = new CastSpellInfo(
            name.Trim(),
            castTimeSeconds.Value,
            icon,
            ReadEdenNumberAttribute(element, "Damage"),
            ReadEdenTextAttribute(element, "Damage Type"),
            className,
            ReadNullableInt(element, "level"),
            HasFixedCastTime(element),
            TryReadEdenAttribute(element, "Recast Delay", out var recast) ? ParseDurationSecondsValue(recast) : null);
        var normalizedName = NormalizeName(name);
        if (!result.TryGetValue(normalizedName, out var entries))
        {
            entries = [];
            result[normalizedName] = entries;
        }
        if (!entries.Contains(candidate))
        {
            entries.Add(candidate);
        }
    }

    private static CastSpellInfo? SelectBest(
        IReadOnlyList<CastSpellInfo>? candidates,
        string? className,
        int? level)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return null;
        }
        IEnumerable<CastSpellInfo> eligible = candidates;
        if (!string.IsNullOrWhiteSpace(className))
        {
            var classMatches = eligible.Where(x => string.Equals(x.ClassName, className, StringComparison.OrdinalIgnoreCase)).ToList();
            if (classMatches.Count > 0)
            {
                eligible = classMatches;
            }
        }
        if (level is not null)
        {
            var levelMatches = eligible.Where(x => x.Level is null || x.Level <= level).ToList();
            if (levelMatches.Count > 0)
            {
                eligible = levelMatches;
            }
        }
        return eligible
            .OrderByDescending(x => x.Level ?? -1)
            .ThenByDescending(Score)
            .FirstOrDefault();
    }

    private static string? ReadEdenTextAttribute(JsonElement element, string attributeName)
    {
        return TryReadEdenAttribute(element, attributeName, out var value) ? value : null;
    }

    private static double? ReadEdenNumberAttribute(JsonElement element, string attributeName)
    {
        if (!TryReadEdenAttribute(element, attributeName, out var value))
        {
            return null;
        }
        var match = System.Text.RegularExpressions.Regex.Match(value, @"-?\d+(?:\.\d+)?");
        return match.Success && double.TryParse(
            match.Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool TryReadEdenAttribute(JsonElement element, string attributeName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty("attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var attribute in attributes.EnumerateArray())
        {
            if (TryReadString(attribute, "name", out var name) &&
                string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) &&
                TryReadString(attribute, "value", out value))
            {
                return true;
            }
        }
        return false;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static double? ParseCastTimeSecondsFromAttributes(JsonElement element)
    {
        if (!element.TryGetProperty("attributes", out var attributesElement) || attributesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var attribute in attributesElement.EnumerateArray())
        {
            if (!TryReadString(attribute, "name", out var name) ||
                !string.Equals(name, "Cast Time", StringComparison.OrdinalIgnoreCase) ||
                !TryReadString(attribute, "value", out var value))
            {
                continue;
            }

            return ParseDurationSecondsValue(value);
        }

        return null;
    }

    private static bool HasAffirmativeAttribute(JsonElement element, string attributeName)
    {
        if (!element.TryGetProperty("attributes", out var attributesElement) ||
            attributesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var attribute in attributesElement.EnumerateArray())
        {
            if (!TryReadString(attribute, "name", out var name) ||
                !string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) ||
                !TryReadString(attribute, "value", out var value))
            {
                continue;
            }

            return value.Trim() is "1" ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool HasFixedCastTime(JsonElement element)
    {
        if (HasAffirmativeAttribute(element, "Fixed Cast Time"))
        {
            return true;
        }

        // Instrument speed pulses are explicitly move-cast songs; their
        // catalogued three-second cast is not modified by dexterity.
        return HasAffirmativeAttribute(element, "Move Cast") &&
               string.Equals(
                   ReadEdenTextAttribute(element, "Type"),
                   "Speed Enhancement",
                   StringComparison.OrdinalIgnoreCase);
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
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static double? ParseCastTimeSeconds(string description)
    {
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
        if (parts.Length != 2)
        {
            return null;
        }

        var raw = parts[1]
            .Replace("seconds", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("second", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return ParseDurationSecondsValue(raw);
    }

    private static double? ParseDurationSecondsValue(string raw)
    {
        // "20:00 min" — mm:ss form used by the delve duration attribute.
        var minuteMatch = System.Text.RegularExpressions.Regex.Match(
            raw, @"(?<mm>\d+):(?<ss>\d+)\s*min", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (minuteMatch.Success)
        {
            return int.Parse(minuteMatch.Groups["mm"].Value) * 60
                   + int.Parse(minuteMatch.Groups["ss"].Value);
        }

        var normalized = raw
            .Replace("minutes", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("minute", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("mins", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("min", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("seconds", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("second", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("secs", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("sec", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("s", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return raw.Contains("min", StringComparison.OrdinalIgnoreCase) ? value * 60 : value;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
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

    private static int ReadNestedInt(JsonElement element, string objectName, string propertyName)
    {
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

    private static string NormalizeName(string value)
    {
        return string.Join(" ", value
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static string StripGenericCastWords(string value)
    {
        var normalized = value.Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"^(?:a|an|the)\s+", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+spell$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return normalized.Trim();
    }

    private static int Score(CastSpellInfo info)
    {
        return (info.Icon is null ? 0 : 10) + (int)Math.Round(info.CastTimeSeconds);
    }

    private sealed record IndexedIcon(IconSpriteRef Icon, string MappingType);
}
