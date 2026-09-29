using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Server-spell table first, ability-profile definitions as
/// fallback — shards whose spell names don't match the server export
/// (Eden/Blackthorn) still resolve pending-CC durations for every ability
/// the user defined, aliases included.</summary>
public sealed class CompositeCcSpellIndex : ICcSpellIndex
{
    private static readonly HashSet<ControlEffectType> CcEffects =
    [
        ControlEffectType.Stun, ControlEffectType.Mezz, ControlEffectType.Root,
        ControlEffectType.Snare, ControlEffectType.Nearsight
    ];

    private readonly ICcSpellIndex _primary;
    private readonly Dictionary<string, CcSpellInfo> _byAbilityName =
        new(StringComparer.OrdinalIgnoreCase);

    public CompositeCcSpellIndex(ICcSpellIndex primary, IReadOnlyCollection<AbilityDefinition> abilities)
    {
        _primary = primary;
        foreach (var ability in abilities)
        {
            if (!CcEffects.Contains(ability.EffectType) || ability.DurationSeconds <= 0)
            {
                continue;
            }
            var info = new CcSpellInfo(ability.EffectType, ability.DurationSeconds);
            _byAbilityName[Normalize(ability.Name)] = info;
            foreach (var alias in ability.Aliases ?? [])
            {
                _byAbilityName[Normalize(alias)] = info;
            }
        }
    }

    public int Count => _byAbilityName.Count;

    public CcSpellInfo? Resolve(string spellName)
    {
        return _primary.Resolve(spellName) ?? _byAbilityName.GetValueOrDefault(Normalize(spellName));
    }

    private static string Normalize(string name) =>
        string.Join(" ", name.Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}
