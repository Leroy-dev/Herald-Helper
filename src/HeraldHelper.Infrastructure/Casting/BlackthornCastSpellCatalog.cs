using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

public sealed class BlackthornCastSpellCatalog : ICastSpellCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> _byName = LoadCatalog();

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
        var result = SelectBest(candidates, className, level);
        if (result is not null)
        {
            return result;
        }
        if (string.IsNullOrWhiteSpace(className))
        {
            return null;
        }
        var classKeys = _byName.Where(x => x.Value.Any(y => string.Equals(y.ClassName, className, StringComparison.OrdinalIgnoreCase))).Select(x => x.Key);
        var fuzzyKey = CastNameMatcher.FindClosest(classKeys, spellName);
        return fuzzyKey is not null && _byName.TryGetValue(fuzzyKey, out candidates)
            ? SelectBest(candidates, className, level)
            : null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> LoadCatalog()
    {
        var dataRoot = TryFindDataRoot();
        var classesDir = dataRoot is null ? null : Path.Combine(dataRoot, "generated", "classes");
        if (classesDir is null || !Directory.Exists(classesDir))
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
                CollectSpells(document.RootElement, className, result);
            }
        }

        return result.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<CastSpellInfo>)x.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void CollectSpells(JsonElement element, string className, Dictionary<string, List<CastSpellInfo>> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TryAddSpell(element, className, result);
                foreach (var property in element.EnumerateObject())
                {
                    CollectSpells(property.Value, className, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectSpells(item, className, result);
                }
                break;
        }
    }

    private static void TryAddSpell(JsonElement element, string className, Dictionary<string, List<CastSpellInfo>> result)
    {
        if (!TryReadString(element, "name", out var name) ||
            !TryReadString(element, "objectType", out var objectType) ||
            !string.Equals(objectType, "Spell", StringComparison.OrdinalIgnoreCase) ||
            !TryReadPositiveDouble(element, "castTime", out var castTime))
        {
            return;
        }

        var icon = ReadIcon(element);
        var candidate = new CastSpellInfo(
            name.Trim(),
            castTime,
            icon,
            ReadPositiveDouble(element, "damage"),
            ReadDamageType(element),
            className,
            ReadNullableInt(element, "level"),
            IsFixedCastTime(element),
            ReadPositiveDouble(element, "recastDelay"),
            ReadPositiveDouble(element, "duration"),
            TryReadString(element, "type", out var type) ? type : null,
            TryReadString(element, "target", out var target) ? target : null,
            ReadNullableInt(element, "effectGroup") == 4);
        var key = NormalizeName(name);
        if (!result.TryGetValue(key, out var entries))
        {
            entries = [];
            result[key] = entries;
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

    private static double? ReadPositiveDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetDouble(out var value) && value > 0
            ? value
            : null;
    }

    private static string? ReadDamageType(JsonElement element)
    {
        if (TryReadString(element, "damageTypeName", out var name))
        {
            return name;
        }
        return element.TryGetProperty("damageType", out var property)
            ? property.ToString()
            : null;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool IsFixedCastTime(JsonElement element) =>
        TryReadString(element, "type", out var type) &&
        string.Equals(type, "SpeedEnhancement", StringComparison.OrdinalIgnoreCase) &&
        TryReadPositiveDouble(element, "frequency", out _);

    private static IconSpriteRef? ReadIcon(JsonElement element)
    {
        if (!element.TryGetProperty("icon", out var iconElement) ||
            !iconElement.TryGetProperty("iconLocation", out var locationElement) ||
            !locationElement.TryGetInt32(out var iconLocation) ||
            iconLocation < 0)
        {
            return null;
        }

        var spriteClass = (iconLocation / 100) * 100;
        var cell = iconLocation % 100;
        var spriteSheet = ResolveSpriteSheet(spriteClass);
        if (spriteSheet is null)
        {
            return null;
        }

        return new IconSpriteRef(
            spriteSheet,
            cell % 10,
            cell / 10,
            32,
            32,
            0,
            0);
    }

    private static string? ResolveSpriteSheet(int spriteClass)
    {
        if (spriteClass >= 0)
        {
            return $"blackthorn/spells/spl_{spriteClass}.bmp";
        }

        return null;
    }

    private static bool TryReadPositiveDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetDouble(out value) && value > 0;
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

    private static string NormalizeName(string value)
    {
        return string.Join(" ", value
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static int Score(CastSpellInfo info)
    {
        return (info.Icon is null ? 0 : 100) + (int)Math.Round(info.CastTimeSeconds);
    }

    private static string? TryFindDataRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "blackthorn-charplan");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
