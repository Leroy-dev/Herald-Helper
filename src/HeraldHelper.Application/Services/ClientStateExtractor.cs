using System.Globalization;
using System.Text.RegularExpressions;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Services;

/// <summary>
/// Projects the raw adapter dictionary (IAdapterValueSource.LatestAdapterValues)
/// into a typed ClientStateSnapshot. Adapter values arrive as client-rendered
/// strings: numbers ("75", "73.5"), percentages ("+14%"), bracket lists
/// ("[Sprint]\[Speed of Sound I]"), and "%d"-indexed group/pet slots.
/// </summary>
public static class ClientStateExtractor
{
    private const int MaxGroupSlots = 8;
    private const int MaxGroupBuffIcons = 30;
    private const int MaxPetSlots = 8;

    public static ClientStateSnapshot Extract(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return ClientStateSnapshot.Empty;
        }

        return new ClientStateSnapshot(
            GroupMembers: ExtractGroupMembers(values),
            Buffs: ExtractSummaryIconDescriptions(values),
            Pet: ExtractPet(values),
            Siege: ExtractSiege(values),
            InCombat: ReadInt(values, "combat_mode") == 1,
            CompassHeading: ReadNumber(values, "compass_heading"),
            RealmPoints: ReadLong(values, "stats_realm_points"),
            BountyPoints: ReadLong(values, "bounty_points"),
            RelicTimePercent: ReadNumber(values, "mino_relic_time_percent"),
            ReleaseTimerSeconds: ReadNumber(values, "release_timer_time"),
            TimerSeconds: ReadNumber(values, "timer_time"));
    }

    private static IReadOnlyList<GroupMemberState> ExtractGroupMembers(IReadOnlyDictionary<string, string> values)
    {
        var members = new List<GroupMemberState>();
        for (var i = 0; i < MaxGroupSlots; i++)
        {
            if (!values.TryGetValue($"group_name{i}", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var icons = new List<string>();
            for (var icon = 0; icon < MaxGroupBuffIcons; icon++)
            {
                if (values.TryGetValue($"group_{i}icon{icon}", out var iconValue) &&
                    !string.IsNullOrWhiteSpace(iconValue) && iconValue != "0")
                {
                    icons.Add(iconValue);
                }
            }

            members.Add(new GroupMemberState(
                i,
                name.Trim(),
                values.TryGetValue($"group_class{i}", out var cls) ? cls.Trim() : string.Empty,
                ReadInt(values, $"group_health{i}"),
                ReadInt(values, $"group_power{i}"),
                ReadInt(values, $"group_endurance{i}"),
                ReadInt(values, $"group_level{i}"),
                values.TryGetValue($"group_zone{i}", out var zone) ? zone.Trim() : null,
                ReadNumber(values, $"group_xpos{i}"),
                ReadNumber(values, $"group_ypos{i}"),
                ReadNumber(values, $"group_zpos{i}"),
                icons));
        }

        return members;
    }

    private static PetState? ExtractPet(IReadOnlyDictionary<string, string> values)
    {
        var hasPet = values.ContainsKey("mini_pet_life") || values.ContainsKey("mini_pet_title");
        if (!hasPet)
        {
            return null;
        }

        return new PetState(
            values.TryGetValue("mini_pet_title", out var title) ? title.Trim() : null,
            ReadInt(values, "mini_pet_life"),
            ExtractIndexList(values, "mini_pet_combat"),
            ExtractIndexList(values, "mini_pet_movement"),
            ExtractPetEffectIcons(values));
    }

    /// <summary>mini_pet_effectN = iconId of each effect currently on the pet —
    /// the same ids the charplan icon configs use. -1 = empty slot.</summary>
    private static IReadOnlyList<int> ExtractPetEffectIcons(IReadOnlyDictionary<string, string> values)
    {
        var icons = new List<int>();
        for (var i = 0; i < MaxPetSlots; i++)
        {
            if (ReadInt(values, $"mini_pet_effect{i}") is int iconId and > 0)
            {
                icons.Add(iconId);
            }
        }

        return icons;
    }

    private static List<int> ExtractIndexList(IReadOnlyDictionary<string, string> values, string prefix)
    {
        var result = new List<int>();
        for (var i = 0; i < MaxPetSlots; i++)
        {
            if (ReadInt(values, $"{prefix}{i}") is > 0)
            {
                result.Add(i);
            }
        }

        return result;
    }

    private static SiegeState? ExtractSiege(IReadOnlyDictionary<string, string> values)
    {
        var timer = ReadNumber(values, "siege_timer");
        var moving = ReadInt(values, "siege_moving") == 1;
        var hits = ReadInt(values, "siege_hits");
        var helper = ReadNumber(values, "siege_helper_timer");
        return timer is null && hits is null && helper is null && !moving
            ? null
            : new SiegeState(timer, moving, hits, helper);
    }

    /// <summary>Active buff/debuff names live in the summary window's icon
    /// grid: summary_icon_desc{row}{col} (two digits each). stats_abil is the
    /// passive-abilities page — NOT buffs — so it is deliberately not used.</summary>
    private static IReadOnlyList<string> ExtractSummaryIconDescriptions(IReadOnlyDictionary<string, string> values)
    {
        var buffs = new List<string>();
        for (var row = 0; row < 10; row++)
        {
            for (var col = 0; col < 10; col++)
            {
                if (values.TryGetValue($"summary_icon_desc{row}{col}", out var desc) &&
                    !string.IsNullOrWhiteSpace(desc))
                {
                    buffs.Add(desc.Split('\n')[0].Trim());
                }
            }
        }

        return buffs;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, string> values, string key) =>
        ReadNumber(values, key) is { } n ? (int)Math.Round(n) : null;

    private static long? ReadLong(IReadOnlyDictionary<string, string> values, string key) =>
        ReadNumber(values, key) is { } n ? (long)Math.Round(n) : null;

    private static double? ReadNumber(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().TrimEnd('%').Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
