using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteDatabaseMigrationsRepository : SqliteRepositoryBase, IDatabaseMigrationsRepository
{
    public SqliteDatabaseMigrationsRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public const int CurrentDatabaseMigrationVersion = 8;

    public int GetDatabaseMigrationVersion()
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void MigrateDatabase()
    {
        using var connection = ConnectionFactory.OpenConnection();
        EnsureMigrationsTable(connection);
        ApplyMigrations(connection);
    }

    private static void EnsureMigrationsTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_utc TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void ApplyMigrations(SqliteConnection connection)
    {
        ApplyMigration(connection, 1, """
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS abilities (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ability_name TEXT NOT NULL,
                skill_code TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL,
                effect_type TEXT NOT NULL
            );
            """);

        ApplyMigration(connection, 2, """
            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        ApplyMigration(connection, 3, """
            CREATE TABLE IF NOT EXISTS eden_entry_overrides (
                entry_key TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                entry_type TEXT NOT NULL,
                class_name TEXT NOT NULL,
                category TEXT NOT NULL,
                level INTEGER NULL,
                cast_time_seconds REAL NULL,
                summary TEXT NOT NULL,
                details TEXT NOT NULL,
                icon_sprite_sheet TEXT NULL,
                icon_x INTEGER NULL,
                icon_y INTEGER NULL,
                icon_width INTEGER NULL,
                icon_height INTEGER NULL,
                icon_border_index INTEGER NULL,
                icon_spell_badge_index INTEGER NULL
            );
            """);

        ApplyMigration(connection, 4, """
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_up_left_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_up_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_up_right_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_right_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_down_right_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_down_index INTEGER NULL;
            ALTER TABLE eden_entry_overrides ADD COLUMN icon_corner_left_index INTEGER NULL;
            """);

        ApplyMigration(connection, 5, """
            CREATE TABLE IF NOT EXISTS ability_profile_entries (
                server TEXT NOT NULL,
                class_name TEXT NOT NULL,
                ability_name TEXT NOT NULL,
                skill_code TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL,
                effect_type TEXT NOT NULL,
                enabled INTEGER NOT NULL,
                category TEXT NOT NULL,
                level INTEGER NULL,
                is_custom INTEGER NOT NULL,
                PRIMARY KEY(server, class_name, ability_name, effect_type)
            );
            """);

        ApplyMigration(connection, 6, """
            CREATE TABLE IF NOT EXISTS ability_profile_overrides (
                server TEXT NOT NULL,
                class_name TEXT NOT NULL,
                character_name TEXT NOT NULL,
                source_ability_name TEXT NOT NULL,
                source_effect_type TEXT NOT NULL,
                enabled_override INTEGER NULL,
                ability_name_override TEXT NULL,
                skill_code_override TEXT NULL,
                duration_seconds_override INTEGER NULL,
                effect_type_override TEXT NULL,
                category_override TEXT NULL,
                level_override INTEGER NULL,
                has_level_override INTEGER NOT NULL,
                aliases_override TEXT NULL,
                is_custom INTEGER NOT NULL,
                PRIMARY KEY(server, class_name, character_name, source_ability_name, source_effect_type)
            );

            INSERT OR IGNORE INTO ability_profile_overrides(
                server, class_name, character_name, source_ability_name, source_effect_type,
                enabled_override, ability_name_override, skill_code_override,
                duration_seconds_override, effect_type_override, category_override,
                level_override, has_level_override, aliases_override, is_custom)
            SELECT server, class_name, '', ability_name, effect_type,
                   enabled, ability_name, skill_code, duration_seconds,
                   effect_type, category, level, 1, NULL, is_custom
            FROM ability_profile_entries;
            """);

        ApplyMigration(connection, 7, """
            CREATE TABLE IF NOT EXISTS character_stats (
                server TEXT NOT NULL,
                character_name TEXT NOT NULL,
                strength INTEGER NULL,
                constitution INTEGER NULL,
                dexterity INTEGER NULL,
                quickness INTEGER NULL,
                intelligence INTEGER NULL,
                piety INTEGER NULL,
                empathy INTEGER NULL,
                charisma INTEGER NULL,
                casting_speed_percent REAL NOT NULL,
                spell_damage_percent REAL NOT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY(server, character_name)
            );
            """);

        ApplyMigration(connection, 8, """
            CREATE TABLE IF NOT EXISTS target_profile_cache (
                server TEXT NOT NULL,
                normalized_name TEXT NOT NULL,
                name TEXT NOT NULL,
                guild_name TEXT NULL,
                class_name TEXT NULL,
                level INTEGER NULL,
                realm_rank TEXT NULL,
                solo_kills INTEGER NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY(server, normalized_name)
            );
            """);
    }

    private static void ApplyMigration(SqliteConnection connection, int version, string sql)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = $v;";
        exists.Parameters.AddWithValue("$v", version);
        var already = Convert.ToInt32(exists.ExecuteScalar() ?? 0) > 0;
        if (already)
        {
            return;
        }

        using var tx = connection.BeginTransaction();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        using (var mark = connection.CreateCommand())
        {
            mark.Transaction = tx;
            mark.CommandText = "INSERT INTO schema_migrations(version, applied_utc) VALUES($v, $utc);";
            mark.Parameters.AddWithValue("$v", version);
            mark.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            mark.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
