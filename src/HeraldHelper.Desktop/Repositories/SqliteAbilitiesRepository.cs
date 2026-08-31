using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteAbilitiesRepository : SqliteRepositoryBase, IAbilityRepository
{
    public SqliteAbilitiesRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public List<AbilityEditorRow> LoadAbilities()
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ability_name, skill_code, duration_seconds, effect_type FROM abilities ORDER BY id;";

        var rows = new List<AbilityEditorRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AbilityEditorRow
            {
                AbilityName = reader.GetString(0),
                SkillCode = reader.GetString(1),
                DurationSeconds = reader.GetInt32(2),
                EffectType = reader.GetString(3)
            });
        }

        return rows;
    }

    public void SaveAbilities(IEnumerable<AbilityEditorRow> rows)
    {
        var list = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.AbilityName))
            .Select(x => new AbilityEditorRow
            {
                AbilityName = x.AbilityName.Trim(),
                SkillCode = NormalizeCode(x.SkillCode, "s"),
                DurationSeconds = Math.Max(1, x.DurationSeconds),
                EffectType = NormalizeCode(x.EffectType, "s")
            })
            .ToList();

        using var connection = ConnectionFactory.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM abilities;";
            delete.ExecuteNonQuery();
        }

        foreach (var row in list)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO abilities(ability_name, skill_code, duration_seconds, effect_type)
                VALUES($n, $s, $d, $e);
                """;
            insert.Parameters.AddWithValue("$n", row.AbilityName);
            insert.Parameters.AddWithValue("$s", row.SkillCode);
            insert.Parameters.AddWithValue("$d", row.DurationSeconds);
            insert.Parameters.AddWithValue("$e", row.EffectType);
            insert.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
