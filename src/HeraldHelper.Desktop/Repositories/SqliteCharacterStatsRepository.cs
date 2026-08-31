using System.Globalization;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteCharacterStatsRepository : SqliteRepositoryBase, ICharacterStatsRepository
{
    public SqliteCharacterStatsRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public CharacterStatsSnapshot? LoadCharacterStats(ShardType shard, string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT strength, constitution, dexterity, quickness, intelligence, piety, empathy, charisma,
                   casting_speed_percent, spell_damage_percent, updated_utc
            FROM character_stats
            WHERE server = $server AND character_name = $character;
            """;
        cmd.Parameters.AddWithValue("$server", shard.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$character", NormalizeProfileSegment(characterName));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new CharacterStatsSnapshot(
            shard,
            characterName.Trim(),
            ReadNullableInt(reader, 0),
            ReadNullableInt(reader, 1),
            ReadNullableInt(reader, 2),
            ReadNullableInt(reader, 3),
            ReadNullableInt(reader, 4),
            ReadNullableInt(reader, 5),
            ReadNullableInt(reader, 6),
            ReadNullableInt(reader, 7),
            reader.GetDouble(8),
            reader.GetDouble(9),
            DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture));
    }

    public void SaveCharacterStats(CharacterStatsSnapshot stats)
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO character_stats(
                server, character_name, strength, constitution, dexterity, quickness,
                intelligence, piety, empathy, charisma, casting_speed_percent,
                spell_damage_percent, updated_utc)
            VALUES($server, $character, $str, $con, $dex, $qui, $int, $pie, $emp, $cha, $cast, $damage, $updated)
            ON CONFLICT(server, character_name) DO UPDATE SET
                strength=excluded.strength, constitution=excluded.constitution,
                dexterity=excluded.dexterity, quickness=excluded.quickness,
                intelligence=excluded.intelligence, piety=excluded.piety,
                empathy=excluded.empathy, charisma=excluded.charisma,
                casting_speed_percent=excluded.casting_speed_percent,
                spell_damage_percent=excluded.spell_damage_percent,
                updated_utc=excluded.updated_utc;
            """;
        cmd.Parameters.AddWithValue("$server", stats.Shard.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$character", NormalizeProfileSegment(stats.CharacterName));
        AddNullableInt(cmd, "$str", stats.Strength);
        AddNullableInt(cmd, "$con", stats.Constitution);
        AddNullableInt(cmd, "$dex", stats.Dexterity);
        AddNullableInt(cmd, "$qui", stats.Quickness);
        AddNullableInt(cmd, "$int", stats.Intelligence);
        AddNullableInt(cmd, "$pie", stats.Piety);
        AddNullableInt(cmd, "$emp", stats.Empathy);
        AddNullableInt(cmd, "$cha", stats.Charisma);
        cmd.Parameters.AddWithValue("$cast", stats.CastingSpeedPercent);
        cmd.Parameters.AddWithValue("$damage", stats.SpellDamagePercent);
        cmd.Parameters.AddWithValue("$updated", stats.UpdatedUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
