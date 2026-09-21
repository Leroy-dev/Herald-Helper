namespace HeraldHelper.Domain.Models;

public enum DamageVerdict
{
    Neutral,
    Weak,
    Resists
}

/// <summary>
/// Class → armor-type melee damage verdicts, ported verbatim from the original
/// ocr.ahk resists overlay (PLATELEATHER/SCALE/CHAINSTUDDED/CHAINMID/
/// REINFORCEDLEAHTER/STUDDEDLEATHER sets). Weak = the target takes extra damage
/// from that type, Resists = reduced. Cloth and unknown classes are neutral to
/// everything. Heretic/Mauler were added — both post-date the original table.
/// </summary>
public static class ClassArmorTable
{
    private static readonly (string[] Classes, DamageVerdict Thrust, DamageVerdict Slash, DamageVerdict Crush)[] Sets =
    [
        // plate-leather: weak thrust, resist crush
        (["Paladin", "Armsman", "Armswoman", "Infiltrator", "Friar"],
            DamageVerdict.Weak, DamageVerdict.Neutral, DamageVerdict.Resists),
        // scale: weak slash, resist thrust
        (["Champion", "Hero", "Heroine", "Druid", "Warden"],
            DamageVerdict.Resists, DamageVerdict.Weak, DamageVerdict.Neutral),
        // chain/studded (Alb): weak crush, resist thrust
        (["Cleric", "Mercenary", "Minstrel", "Reaver", "Scout", "Heretic"],
            DamageVerdict.Resists, DamageVerdict.Neutral, DamageVerdict.Weak),
        // chain (Mid): weak crush, resist slash
        (["Healer", "Shaman", "Skald", "Thane", "Warrior", "Valkyrie"],
            DamageVerdict.Neutral, DamageVerdict.Resists, DamageVerdict.Weak),
        // reinforced leather: weak thrust, resist slash
        (["Bard", "Blademaster", "Ranger", "Nightshade", "Vampiir"],
            DamageVerdict.Weak, DamageVerdict.Resists, DamageVerdict.Neutral),
        // studded leather: weak slash, resist crush
        (["Berserker", "Hunter", "Savage", "Shadowblade", "Mauler"],
            DamageVerdict.Neutral, DamageVerdict.Weak, DamageVerdict.Resists),
    ];

    /// <summary>Returns (thrust, slash, crush) verdicts for a class name —
    /// all neutral for cloth wearers and anything unrecognized.</summary>
    public static (DamageVerdict Thrust, DamageVerdict Slash, DamageVerdict Crush) Lookup(string? className)
    {
        if (!string.IsNullOrWhiteSpace(className))
        {
            foreach (var (classes, thrust, slash, crush) in Sets)
            {
                if (classes.Any(c => string.Equals(c, className.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return (thrust, slash, crush);
                }
            }
        }

        return (DamageVerdict.Neutral, DamageVerdict.Neutral, DamageVerdict.Neutral);
    }
}
