using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Parsing;

public sealed record AbilityProfileDefinition(
    ShardType Shard,
    string ClassName,
    string Name,
    string SkillCode,
    int DurationSeconds,
    ControlEffectType EffectType,
    string Category,
    int? Level);

public static partial class AbilityProfileCatalog
{
    private static readonly object CacheLock = new();
    private static IReadOnlyList<AbilityProfileDefinition>? _edenEntries;
    private static IReadOnlyList<AbilityProfileDefinition>? _blackthornEntries;
    private static IReadOnlyList<string>? _edenClasses;
    private static IReadOnlyList<string>? _blackthornClasses;

    public static IReadOnlyList<string> GetClasses(ShardType shard)
    {
        return shard switch
        {
            ShardType.Eden => GetCachedClasses(ShardType.Eden),
            ShardType.Blackthorn => GetCachedClasses(ShardType.Blackthorn),
            _ => []
        };
    }

    public static void Refresh()
    {
        lock (CacheLock)
        {
            _edenEntries = null;
            _blackthornEntries = null;
            _edenClasses = null;
            _blackthornClasses = null;
        }
    }

    public static IReadOnlyList<AbilityProfileDefinition> GetProfile(ShardType shard, string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return [];
        }

        return GetEntries(shard)
            .Where(x => string.Equals(x.ClassName, className.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Level ?? int.MaxValue)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AbilityProfileDefinition> GetEntries(ShardType shard)
    {
        lock (CacheLock)
        {
            return shard switch
            {
                ShardType.Eden => _edenEntries ??= LoadShard(
                    ShardType.Eden,
                    "eden-charplan",
                    (element, cls, shard, result) => CollectEdenEntries(element, cls, shard, result)),
                ShardType.Blackthorn => _blackthornEntries ??= LoadShard(ShardType.Blackthorn, "blackthorn-charplan", CollectBlackthornEntries),
                _ => []
            };
        }
    }

    private static IReadOnlyList<string> GetCachedClasses(ShardType shard)
    {
        lock (CacheLock)
        {
            return shard switch
            {
                ShardType.Eden => _edenClasses ??= LoadClassNames("eden-charplan"),
                ShardType.Blackthorn => _blackthornClasses ??= LoadClassNames("blackthorn-charplan"),
                _ => []
            };
        }
    }

    private static IReadOnlyList<AbilityProfileDefinition> LoadShard(
        ShardType shard,
        string directoryName,
        Action<JsonElement, string, ShardType, List<AbilityProfileDefinition>> collector)
    {
        var dataRoot = TryFindDataRoot(directoryName);
        var classesDir = dataRoot is null ? null : Path.Combine(dataRoot, "generated", "classes");
        if (classesDir is null || !Directory.Exists(classesDir))
        {
            return [];
        }

        var candidates = new List<AbilityProfileDefinition>();
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

            collector(root, className, shard, candidates);
        }

        return candidates
            .GroupBy(x => $"{x.ClassName}|{x.Name}|{x.EffectType}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.Level ?? -1)
                .ThenByDescending(x => x.DurationSeconds)
                .First())
            .OrderBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> LoadClassNames(string directoryName)
    {
        var dataRoot = TryFindDataRoot(directoryName);
        var indexPath = dataRoot is null
            ? null
            : Path.Combine(dataRoot, "generated", "classes", "index.json");
        if (indexPath is null || !File.Exists(indexPath))
        {
            return [];
        }

        using var stream = File.OpenRead(indexPath);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement
            .EnumerateArray()
            .Select(x => TryReadString(x, "name", out var name) ? name.Trim() : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectEdenEntries(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result,
        string? parentNameOverride = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TryAddEdenSkill(element, className, shard, result, parentNameOverride);
                TryAddEdenRealmAbility(element, className, shard, result);
                foreach (var property in element.EnumerateObject())
                {
                    // Melee style data keeps the style name on the skill and the
                    // applied-effect descriptor ("Stun, 7sec") on its subSkills —
                    // the ability should carry the style name, not the effect text.
                    var childOverride = property.NameEquals("subSkills") &&
                                        TryReadString(element, "name", out var parentName)
                        ? parentName
                        : null;
                    CollectEdenEntries(property.Value, className, shard, result, childOverride);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectEdenEntries(item, className, shard, result, parentNameOverride);
                }
                break;
        }
    }

    private static void TryAddEdenSkill(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result,
        string? nameOverride = null)
    {
        if (!TryReadString(element, "name", out var name) ||
            !element.TryGetProperty("attributes", out var attributes) ||
            attributes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var type = ReadEdenAttribute(attributes, "Type");
        var durationRaw = ReadEdenAttribute(attributes, "Duration");
        if (!TryMapEffect(type, string.Empty, out var effectType) ||
            !TryParseDuration(durationRaw, out var durationSeconds))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(nameOverride))
        {
            name = nameOverride;
        }

        result.Add(CreateDefinition(
            shard,
            className,
            name,
            durationSeconds,
            effectType,
            type,
            ReadNullableInt(element, "level")));
    }

    private static void TryAddEdenRealmAbility(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result)
    {
        if (!TryReadString(element, "name", out var name) ||
            !TryReadString(element, "description", out var description) ||
            !element.TryGetProperty("costScheme", out _))
        {
            return;
        }

        if (!TryMapEffect(string.Empty, description, out var effectType) ||
            !TryParseControlDuration(description, effectType, out var durationSeconds))
        {
            return;
        }

        result.Add(CreateDefinition(
            shard,
            className,
            name,
            durationSeconds,
            effectType,
            "Realm Ability",
            null));
    }

    private static void CollectBlackthornEntries(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TryAddBlackthornSkill(element, className, shard, result);
                TryAddBlackthornRealmAbility(element, className, shard, result);
                foreach (var property in element.EnumerateObject())
                {
                    CollectBlackthornEntries(property.Value, className, shard, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectBlackthornEntries(item, className, shard, result);
                }
                break;
        }
    }

    private static void TryAddBlackthornSkill(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result)
    {
        if (!TryReadString(element, "name", out var name) ||
            !TryReadString(element, "objectType", out var objectType) ||
            (objectType != "Spell" && objectType != "Style"))
        {
            return;
        }

        var type = TryReadString(element, "type", out var typeValue) ? typeValue : string.Empty;
        var description = TryReadString(element, "description", out var descriptionValue) ? descriptionValue : string.Empty;
        if (!TryMapEffect(type, description, out var effectType))
        {
            return;
        }

        var durationSeconds = ReadPositiveInt(element, "duration");
        if (durationSeconds <= 0 && !TryParseControlDuration(description, effectType, out durationSeconds))
        {
            return;
        }

        result.Add(CreateDefinition(
            shard,
            className,
            name,
            durationSeconds,
            effectType,
            objectType,
            ReadNullableInt(element, "level")));
    }

    private static void TryAddBlackthornRealmAbility(
        JsonElement element,
        string className,
        ShardType shard,
        List<AbilityProfileDefinition> result)
    {
        if (!TryReadString(element, "name", out var name) ||
            !TryReadString(element, "delve", out var delve) ||
            !element.TryGetProperty("costs", out _))
        {
            return;
        }

        if (!TryMapEffect(string.Empty, delve, out var effectType) ||
            !TryParseControlDuration(delve, effectType, out var durationSeconds))
        {
            return;
        }

        result.Add(CreateDefinition(
            shard,
            className,
            name,
            durationSeconds,
            effectType,
            "Realm Ability",
            null));
    }

    private static AbilityProfileDefinition CreateDefinition(
        ShardType shard,
        string className,
        string name,
        int durationSeconds,
        ControlEffectType effectType,
        string category,
        int? level)
    {
        var skillCode = effectType switch
        {
            ControlEffectType.Mezz => "m",
            ControlEffectType.Root => "r",
            _ => "s"
        };
        return new AbilityProfileDefinition(
            shard,
            className.Trim(),
            name.Trim(),
            skillCode,
            Math.Max(1, durationSeconds),
            effectType,
            string.IsNullOrWhiteSpace(category) ? effectType.ToString() : category.Trim(),
            level);
    }

    private static bool TryMapEffect(string type, string description, out ControlEffectType effectType)
    {
        var text = $"{type} {description}";
        if (text.Contains("immunity", StringComparison.OrdinalIgnoreCase))
        {
            effectType = default;
            return false;
        }

        if (MezzRegex().IsMatch(text))
        {
            effectType = ControlEffectType.Mezz;
            return true;
        }

        if (RootRegex().IsMatch(text))
        {
            effectType = ControlEffectType.Root;
            return true;
        }

        if (StunRegex().IsMatch(text))
        {
            effectType = ControlEffectType.Stun;
            return true;
        }

        effectType = default;
        return false;
    }

    private static bool TryParseDuration(string? raw, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var minuteSecond = MinuteSecondRegex().Match(raw);
        if (minuteSecond.Success &&
            int.TryParse(minuteSecond.Groups["minutes"].Value, out var minutes) &&
            int.TryParse(minuteSecond.Groups["seconds"].Value, out var remainingSeconds))
        {
            seconds = (minutes * 60) + remainingSeconds;
            return seconds > 0;
        }

        var second = SecondsRegex().Match(raw);
        if (second.Success &&
            double.TryParse(second.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var secondValue))
        {
            seconds = (int)Math.Round(secondValue, MidpointRounding.AwayFromZero);
            return seconds > 0;
        }

        var minute = MinutesRegex().Match(raw);
        if (minute.Success &&
            double.TryParse(minute.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minuteValue))
        {
            seconds = (int)Math.Round(minuteValue * 60, MidpointRounding.AwayFromZero);
            return seconds > 0;
        }

        return false;
    }

    private static bool TryParseControlDuration(
        string text,
        ControlEffectType effectType,
        out int seconds)
    {
        seconds = 0;
        var match = effectType switch
        {
            ControlEffectType.Mezz => MezzDurationRegex().Match(text),
            ControlEffectType.Root => RootDurationRegex().Match(text),
            _ => StunDurationRegex().Match(text)
        };
        return match.Success && TryParseDuration(match.Groups["duration"].Value, out seconds);
    }

    private static string ReadEdenAttribute(JsonElement attributes, string attributeName)
    {
        foreach (var attribute in attributes.EnumerateArray())
        {
            if (TryReadString(attribute, "name", out var name) &&
                string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) &&
                TryReadString(attribute, "value", out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static int ReadPositiveInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.TryGetInt32(out var integer))
        {
            return Math.Max(0, integer);
        }

        return property.TryGetDouble(out var number)
            ? Math.Max(0, (int)Math.Round(number, MidpointRounding.AwayFromZero))
            : 0;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
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

    [GeneratedRegex(@"\b(?:mesmeri[sz]\w*|mezz\w*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MezzRegex();

    [GeneratedRegex(@"\broot\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex RootRegex();

    [GeneratedRegex(@"\bstun\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex StunRegex();

    [GeneratedRegex(@"(?<minutes>\d+)\s*:\s*(?<seconds>\d{1,2})\s*(?:min(?:ute)?s?)?", RegexOptions.IgnoreCase)]
    private static partial Regex MinuteSecondRegex();

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)\s*(?:s|sec(?:ond)?s?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SecondsRegex();

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)\s*min(?:ute)?s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex MinutesRegex();

    [GeneratedRegex(@"\b(?:mesmeri[sz]\w*|mezz\w*)[^.!?\r\n]{0,160}?\bfor\s+(?<duration>\d+(?::\d{1,2})?\s*(?:s|sec(?:ond)?s?|min(?:ute)?s?))", RegexOptions.IgnoreCase)]
    private static partial Regex MezzDurationRegex();

    [GeneratedRegex(@"\broot\w*[^.!?\r\n]{0,160}?\bfor\s+(?<duration>\d+(?::\d{1,2})?\s*(?:s|sec(?:ond)?s?|min(?:ute)?s?))", RegexOptions.IgnoreCase)]
    private static partial Regex RootDurationRegex();

    [GeneratedRegex(@"\bstun\w*[^.!?\r\n]{0,160}?\bfor\s+(?<duration>\d+(?::\d{1,2})?\s*(?:s|sec(?:ond)?s?|min(?:ute)?s?))", RegexOptions.IgnoreCase)]
    private static partial Regex StunDurationRegex();
}
