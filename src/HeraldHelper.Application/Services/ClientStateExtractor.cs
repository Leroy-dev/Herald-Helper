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
    private static readonly Regex ListEntry = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    public static ClientStateSnapshot Extract(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return ClientStateSnapshot.Empty;
        }

        return new ClientStateSnapshot(
            GroupMembers: ExtractGroupMembers(values),
            Buffs: ExtractList(values, "stats_abil"),
            ConcentrationBuffs: ExtractList(values, "conc_list"),
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
            ExtractIndexList(values, "mini_pet_movement"));
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

    /// <summary>"[Sprint]\[Speed of Sound I|5]\[Glacial Movement]" → names.</summary>
    private static IReadOnlyList<string> ExtractList(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var entries = new List<string>();
        foreach (Match match in ListEntry.Matches(raw))
        {
            var name = match.Groups[1].Value.Split('|')[0].Trim();
            if (name.Length > 0)
            {
                entries.Add(name);
            }
        }

        return entries;
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
