using System.Globalization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>CC-effect index over data/client-tables/server-spells.csv — the
/// Type column classifies each spell (Stun/Mesmerize/SpeedDecrease/…) and
/// Duration is in seconds. DamageSpeedDecrease spells print snare-looking
/// lines but carry no immunity, so they deliberately resolve to nothing.</summary>
public sealed class ServerCcSpellIndex : ICcSpellIndex
{
    private readonly IReadOnlyDictionary<string, CcSpellInfo> _byName;

    public ServerCcSpellIndex()
    {
        _byName = Load();
    }

    public CcSpellInfo? Resolve(string spellName)
    {
        return !string.IsNullOrWhiteSpace(spellName) &&
               _byName.TryGetValue(NormalizeName(spellName), out var info)
            ? info
            : null;
    }

    private static IReadOnlyDictionary<string, CcSpellInfo> Load()
    {
        var result = new Dictionary<string, CcSpellInfo>(StringComparer.OrdinalIgnoreCase);
        var dataRoot = ServerCastSpellCatalog.FindDataRoot();
        var tablePath = dataRoot is null
            ? null
            : Path.Combine(dataRoot, "client-tables", "server-spells.csv");
        if (tablePath is null || !File.Exists(tablePath))
        {
            return result;
        }

        foreach (var line in File.ReadLines(tablePath).Skip(1))
        {
            var fields = ServerCastSpellCatalog.ParseCsvLine(line);
            if (fields.Count < 5 || string.IsNullOrWhiteSpace(fields[1]))
            {
                continue;
            }

            if (EffectForType(fields[2].Trim()) is not { } effect ||
                !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) ||
                durationSeconds <= 0)
            {
                continue;
            }

            var key = NormalizeName(fields[1].Trim());
            // Duplicate names are rank/variant rows sharing the same duration.
            if (!result.ContainsKey(key))
            {
                result[key] = new CcSpellInfo(effect, (int)Math.Round(durationSeconds));
            }
        }

        return result;
    }

    private static ControlEffectType? EffectForType(string type)
    {
        return type switch
        {
            "Stun" or "StyleStun" => ControlEffectType.Stun,
            "Mesmerize" => ControlEffectType.Mezz,
            // SpeedDecrease covers snare and root - same immunity bucket.
            "SpeedDecrease" or "UnbreakableSpeedDecrease" => ControlEffectType.Snare,
            "Nearsight" => ControlEffectType.Nearsight,
            _ => null
        };
    }

    private static string NormalizeName(string value)
    {
        return string.Join(" ", value
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }
}
