using System.Globalization;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Shards without a charplan catalog (Default/Phoenix/Titan/…) fall
/// back to the manual ability table — when that's empty too, the parser
/// recognizes nothing. Synthesize a generic CC ability set from
/// server-spells.csv so cast/style lines still register; durations come from
/// the same rows the CC index resolves.</summary>
public static class ServerAbilitySynthesis
{
    public static IReadOnlyList<AbilityDefinition> Load()
    {
        var tablePath = DataPaths.FindFile("client-tables", "server-spells.csv");
        if (tablePath is null)
        {
            return [];
        }

        var byName = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(tablePath).Skip(1))
        {
            var fields = ServerCastSpellCatalog.ParseCsvLine(line);
            if (fields.Count < 5 || string.IsNullOrWhiteSpace(fields[1]))
            {
                continue;
            }

            if (ServerCcSpellIndex.EffectForType(fields[2].Trim()) is not { } effect ||
                !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) ||
                durationSeconds <= 0)
            {
                continue;
            }

            var name = fields[1].Trim();
            var seconds = (int)Math.Round(durationSeconds);
            var skill = ServerCcSpellIndex.IsStyleType(fields[2].Trim()) ? "m" : "s";
            // Rank variants share the name — keep the longest duration.
            if (!byName.TryGetValue(name, out var existing) || existing.DurationSeconds < seconds)
            {
                byName[name] = new AbilityDefinition(name, skill, seconds, effect);
            }
        }

        return byName.Values.ToList();
    }
}
