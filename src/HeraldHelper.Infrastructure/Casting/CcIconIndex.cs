using System.Globalization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Icon-id → CC-effect map derived from the server's Spell table
/// (data/client-tables/server-spells.csv). An icon counts as CC evidence only
/// when every spell using it is crowd-control-shaped — icons shared with
/// unrelated spells (e.g. pet summons sharing a snare glyph) are dropped so a
/// group-frame badge never false-positives.</summary>
public sealed class CcIconIndex : ICcIconIndex
{
    private static readonly IReadOnlyDictionary<string, ControlEffectType> TypeMap =
        new Dictionary<string, ControlEffectType>(StringComparer.Ordinal)
        {
            ["Stun"] = ControlEffectType.Stun,
            ["StyleStun"] = ControlEffectType.Stun,
            ["Mesmerize"] = ControlEffectType.Mezz,
            ["MesmerizeDurationBuff"] = ControlEffectType.Mezz,
            ["UnbreakableSpeedDecrease"] = ControlEffectType.Root,
            ["SpeedDecrease"] = ControlEffectType.Snare,
            ["DamageSpeedDecrease"] = ControlEffectType.Snare,
            ["StyleSpeedDecrease"] = ControlEffectType.Snare,
            ["Nearsight"] = ControlEffectType.Nearsight,
        };

    private readonly IReadOnlyDictionary<int, ControlEffectType> _byIcon;

    private CcIconIndex(IReadOnlyDictionary<int, ControlEffectType> byIcon)
    {
        _byIcon = byIcon;
    }

    public static CcIconIndex Empty { get; } = new(new Dictionary<int, ControlEffectType>());

    public ControlEffectType? Resolve(int iconId)
    {
        return _byIcon.TryGetValue(iconId, out var effect) ? effect : null;
    }

    public static CcIconIndex Load()
    {
        var dataRoot = ServerCastSpellCatalog.FindDataRoot();
        var tablePath = dataRoot is null
            ? null
            : Path.Combine(dataRoot, "client-tables", "server-spells.csv");
        if (tablePath is null || !File.Exists(tablePath))
        {
            return Empty;
        }

        var iconTypes = new Dictionary<int, HashSet<string>>();
        foreach (var line in File.ReadLines(tablePath).Skip(1))
        {
            var fields = ServerCastSpellCatalog.ParseCsvLine(line);
            if (fields.Count < 9 || string.IsNullOrWhiteSpace(fields[2]))
            {
                continue;
            }

            // The strip may carry either the Icon or ClientEffect column's id —
            // index both so either convention resolves.
            foreach (var col in new[] { 8, 9 })
            {
                if (col >= fields.Count ||
                    !int.TryParse(fields[col], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iconId) ||
                    iconId <= 0)
                {
                    continue;
                }

                if (!iconTypes.TryGetValue(iconId, out var types))
                {
                    types = new HashSet<string>(StringComparer.Ordinal);
                    iconTypes[iconId] = types;
                }
                types.Add(fields[2].Trim());
            }
        }

        var byIcon = new Dictionary<int, ControlEffectType>();
        foreach (var (iconId, types) in iconTypes)
        {
            if (types.Any(t => !TypeMap.ContainsKey(t)))
            {
                continue; // icon shared with a non-CC spell — ambiguous, drop it
            }

            // several CC kinds can share one icon; badge the strongest present
            var effect = types
                .Select(t => TypeMap[t])
                .OrderByDescending(Severity)
                .First();
            byIcon[iconId] = effect;
        }

        return new CcIconIndex(byIcon);
    }

    private static int Severity(ControlEffectType effect)
    {
        return effect switch
        {
            ControlEffectType.Stun => 5,
            ControlEffectType.Mezz => 4,
            ControlEffectType.Root => 3,
            ControlEffectType.Snare => 2,
            ControlEffectType.Nearsight => 1,
            _ => 0,
        };
    }
}
