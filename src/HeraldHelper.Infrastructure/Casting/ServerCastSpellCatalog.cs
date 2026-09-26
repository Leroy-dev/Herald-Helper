using System.Globalization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Cast-spell catalog backed by the server's own Spell table —
/// data/client-tables/server-spells.csv (exported from opendaoc.sqlite3.db):
/// SpellID, Name, Type, CastTime(s), Duration(ms), RecastDelay(ms), Damage,
/// DamageType, Icon, ClientEffect. Authoritative for the OpenDAoC shard and a
/// better-than-empty fallback for other freeshards — the charplan catalogs
/// carry Eden/Blackthorn values that don't apply.</summary>
public sealed class ServerCastSpellCatalog : ICastSpellCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> _byName;

    public ServerCastSpellCatalog()
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

        if (_byName.TryGetValue(NormalizeName(spellName), out var candidates))
        {
            return SelectBest(candidates);
        }

        return _byName.TryGetValue(NormalizeName(StripGenericCastWords(spellName)), out candidates)
            ? SelectBest(candidates)
            : null;
    }

    private static CastSpellInfo SelectBest(IReadOnlyList<CastSpellInfo> candidates)
    {
        // Duplicate names are rank/variant rows — prefer the one carrying a
        // real recast so cooldown tracking keeps working.
        return candidates
            .OrderByDescending(x => x.RecastSeconds > 0)
            .ThenByDescending(x => x.Icon is not null)
            .First();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CastSpellInfo>> LoadCatalog()
    {
        var result = new Dictionary<string, List<CastSpellInfo>>(StringComparer.OrdinalIgnoreCase);
        var dataRoot = FindDataRoot();
        var tablePath = dataRoot is null
            ? null
            : Path.Combine(dataRoot, "client-tables", "server-spells.csv");
        if (tablePath is null || !File.Exists(tablePath))
        {
            return result.ToDictionary(x => x.Key, x => (IReadOnlyList<CastSpellInfo>)x.Value);
        }

        var iconIndex = CatalogIconIndex.Load(
            Path.Combine(dataRoot!, "eden-charplan", "generated", "icon-configs.json"));

        foreach (var line in File.ReadLines(tablePath).Skip(1))
        {
            var fields = ParseCsvLine(line);
            if (fields.Count < 10 || string.IsNullOrWhiteSpace(fields[1]))
            {
                continue;
            }

            var name = fields[1].Trim();
            double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var castSeconds);
            long.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var recastMs);
            double.TryParse(fields[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var damage);

            IconSpriteRef? icon = null;
            var iconId = int.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iconCol) && iconCol > 0
                ? iconCol
                : int.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clientFx) ? clientFx : 0;
            if (iconId > 0 && iconIndex.TryGetValue(iconId, out var indexed))
            {
                icon = indexed.Icon;
            }

            var info = new CastSpellInfo(
                name,
                castSeconds,
                icon,
                damage > 0 ? damage : null,
                null,
                null,
                null,
                false,
                recastMs > 0 ? recastMs / 1000.0 : null);

            var key = NormalizeName(name);
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }
            list.Add(info);
        }

        return result.ToDictionary(x => x.Key, x => (IReadOnlyList<CastSpellInfo>)x.Value);
    }

    internal static string? FindDataRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length)
            {
                fields.Add(string.Empty);
                break;
            }

            if (line[i] == '"')
            {
                var start = ++i;
                var value = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            value.Append('"');
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    value.Append(line[i++]);
                }
                fields.Add(value.ToString());
                if (i < line.Length && line[i] == ',')
                {
                    i++;
                }
            }
            else
            {
                var comma = line.IndexOf(',', i);
                if (comma < 0)
                {
                    fields.Add(line[i..]);
                    break;
                }
                fields.Add(line[i..comma]);
                i = comma + 1;
            }
        }
        return fields;
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
}
