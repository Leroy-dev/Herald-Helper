using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteTargetProfileCache : SqliteRepositoryBase, ITargetProfileCache
{
    public SqliteTargetProfileCache(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public TargetProfile? Load(ShardType shardType, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT name, guild_name, class_name, level, realm_rank, solo_kills
            FROM target_profile_cache
            WHERE server = $server AND normalized_name = $name;
            """;
        cmd.Parameters.AddWithValue("$server", shardType.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$name", NormalizeProfileSegment(targetName));
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new TargetProfile(
                reader.GetString(0),
                ReadNullableString(reader, 1),
                ReadNullableString(reader, 2),
                ReadNullableInt(reader, 3),
                ReadNullableString(reader, 4),
                ReadNullableInt(reader, 5))
            : null;
    }

    public IReadOnlyList<TargetProfileRow> Search(string? server, string? nameFragment, int limit = 200)
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT server, name, guild_name, class_name, level, realm_rank, solo_kills, updated_utc
            FROM target_profile_cache
            WHERE ($server IS NULL OR server = $server)
              AND ($frag IS NULL OR normalized_name LIKE '%' || $frag || '%')
            ORDER BY updated_utc DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$server", string.IsNullOrWhiteSpace(server) ? DBNull.Value : server.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$frag", string.IsNullOrWhiteSpace(nameFragment) ? DBNull.Value : NormalizeProfileSegment(nameFragment));
        cmd.Parameters.AddWithValue("$limit", limit);

        var rows = new List<TargetProfileRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new TargetProfileRow(
                reader.GetString(0),
                reader.GetString(1),
                ReadNullableString(reader, 2),
                ReadNullableString(reader, 3),
                ReadNullableInt(reader, 4),
                ReadNullableString(reader, 5),
                ReadNullableInt(reader, 6),
                reader.GetString(7)));
        }

        return rows;
    }

    public void Save(ShardType shardType, TargetProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return;
        }

        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO target_profile_cache(
                server, normalized_name, name, guild_name, class_name, level, realm_rank, solo_kills, updated_utc)
            VALUES($server, $normalized, $name, $guild, $class, $level, $rank, $solo, $updated)
            ON CONFLICT(server, normalized_name) DO UPDATE SET
                name=excluded.name, guild_name=excluded.guild_name, class_name=excluded.class_name,
                level=excluded.level, realm_rank=excluded.realm_rank, solo_kills=excluded.solo_kills,
                updated_utc=excluded.updated_utc;
            """;
        cmd.Parameters.AddWithValue("$server", shardType.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$normalized", NormalizeProfileSegment(profile.Name));
        cmd.Parameters.AddWithValue("$name", profile.Name.Trim());
        AddNullableString(cmd, "$guild", profile.Guild);
        AddNullableString(cmd, "$class", profile.Class);
        AddNullableInt(cmd, "$level", profile.Level);
        AddNullableString(cmd, "$rank", profile.RealmRank);
        AddNullableInt(cmd, "$solo", profile.SoloKills);
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Delete(ShardType shardType, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return;
        }

        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM target_profile_cache
            WHERE server = $server AND normalized_name = $name;
            """;
        cmd.Parameters.AddWithValue("$server", shardType.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$name", NormalizeProfileSegment(targetName));
        cmd.ExecuteNonQuery();
    }
}
