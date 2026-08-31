using System.Text.RegularExpressions;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

public static partial class CharacterStatsParser
{
    public static CharacterStatsSnapshot? Parse(
        string ocrText,
        ShardType shard,
        string characterName,
        CharacterStatsSnapshot? previous,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(ocrText) || string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in StatRegex().Matches(ocrText))
        {
            var label = NormalizeLabel(match.Groups["label"].Value);
            if (int.TryParse(match.Groups["value"].Value, out var value) && value is >= 1 and <= 999)
            {
                values[label] = value;
            }
        }

        if (!values.ContainsKey("dex") || values.Count < 4)
        {
            return null;
        }

        var castSpeed = ReadPercent(CastSpeedRegex(), ocrText) ?? previous?.CastingSpeedPercent ?? 0;
        var spellDamage = ReadPercent(SpellDamageRegex(), ocrText) ?? previous?.SpellDamagePercent ?? 0;
        return new CharacterStatsSnapshot(
            shard,
            characterName.Trim(),
            Get(values, "str"),
            Get(values, "con"),
            Get(values, "dex"),
            Get(values, "qui"),
            Get(values, "int"),
            Get(values, "pie"),
            Get(values, "emp"),
            Get(values, "cha"),
            Math.Clamp(castSpeed, 0, 25),
            Math.Clamp(spellDamage, 0, 25),
            nowUtc);
    }

    public static string StabilityKey(CharacterStatsSnapshot value)
    {
        return $"{value.Strength}|{value.Constitution}|{value.Dexterity}|{value.Quickness}|" +
               $"{value.Intelligence}|{value.Piety}|{value.Empathy}|{value.Charisma}|" +
               $"{value.CastingSpeedPercent:0.##}|{value.SpellDamagePercent:0.##}";
    }

    private static int? Get(IReadOnlyDictionary<string, int> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static string NormalizeLabel(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "lnt" => "int",
            "st" => "str",
            "co" => "con",
            "de" => "dex",
            "qu" => "qui",
            "in" => "int",
            "ch" => "cha",
            "pi" => "pie",
            "em" => "emp",
            "dexx" => "dex",
            "dez" => "dex",
            "coh" => "con",
            _ => value.Trim().ToLowerInvariant()
        };
    }

    private static double? ReadPercent(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success && double.TryParse(
            match.Groups["value"].Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    [GeneratedRegex(@"(?<![A-Za-z])(?<label>Str|Con|Coh|Dexx?|Dez|Qui|Int|lnt|Pie|Emp|Cha|ST|CO|DE|QU|IN|CH|PI|EM)[\s:;.h]*(?<value>\d{1,3})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex StatRegex();

    [GeneratedRegex(@"(?:casting|cast)\s*speed\s*[:;.]?\s*\+?(?<value>\d+(?:[.,]\d+)?)\s*%?", RegexOptions.IgnoreCase)]
    private static partial Regex CastSpeedRegex();

    [GeneratedRegex(@"spell\s*damage\s*[:;.]?\s*\+?(?<value>\d+(?:[.,]\d+)?)\s*%?", RegexOptions.IgnoreCase)]
    private static partial Regex SpellDamageRegex();
}
