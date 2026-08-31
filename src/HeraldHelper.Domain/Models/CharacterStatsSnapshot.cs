using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

public sealed record CharacterStatsSnapshot(
    ShardType Shard,
    string CharacterName,
    int? Strength,
    int? Constitution,
    int? Dexterity,
    int? Quickness,
    int? Intelligence,
    int? Piety,
    int? Empathy,
    int? Charisma,
    double CastingSpeedPercent,
    double SpellDamagePercent,
    DateTimeOffset UpdatedUtc)
{
    public int? ResolveAcuity(string? className)
    {
        var normalized = className?.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "minstrel" or "bard" or "skald" => Charisma,

            "cleric" or "friar" or "heretic" or "reaver" or
            "healer" or "shaman" or "runemaster" or "spiritmaster" or
            "bonedancer" or "warlock" or "thane" or "valkyrie" => Piety,

            "druid" or "warden" => Empathy,

            "cabalist" or "necromancer" or "sorcerer" or "theurgist" or "wizard" or
            "animist" or "bainshee" or "champion" or "eldritch" or "enchanter" or
            "mentalist" or "valewalker" => Intelligence,

            _ => null
        };
    }
}
