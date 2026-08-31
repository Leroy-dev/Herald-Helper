using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Parsing;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop;

public sealed class AppDataStore : ITargetProfileCache, ISettingsRepository, ICharacterStatsRepository, IAbilityRepository, ICatalogOverrideRepository, IAbilityProfileRepository
{
    public const int CurrentDatabaseMigrationVersion = 8;
    public const int CurrentBackupFormatVersion = 1;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSettingsRepository _settingsRepository;
    private readonly SqliteCharacterStatsRepository _characterStatsRepository;
    private readonly SqliteAbilitiesRepository _abilitiesRepository;
    private readonly SqliteTargetProfileCache _targetProfileCache;
    private readonly SqliteCatalogOverrideRepository _catalogOverrideRepository;
    private readonly SqliteAbilityProfileRepository _abilityProfileRepository;

    public AppDataStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var connectionString = $"Data Source={databasePath}";
        _connectionFactory = new SqliteConnectionFactory(connectionString);
        _settingsRepository = new SqliteSettingsRepository(_connectionFactory);
        _characterStatsRepository = new SqliteCharacterStatsRepository(_connectionFactory);
        _abilitiesRepository = new SqliteAbilitiesRepository(_connectionFactory);
        _targetProfileCache = new SqliteTargetProfileCache(_connectionFactory);
        _catalogOverrideRepository = new SqliteCatalogOverrideRepository(_connectionFactory);
        _abilityProfileRepository = new SqliteAbilityProfileRepository(_connectionFactory);
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.OpenConnection();
        EnsureMigrationsTable(connection);
        ApplyMigrations(connection);
    }

    public int GetDatabaseMigrationVersion()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public Dictionary<string, string> LoadSettingsMap()
    {
        return _settingsRepository.LoadSettingsMap();
    }

    public List<ConfigEntry> LoadConfigEntries()
    {
        return _settingsRepository.LoadConfigEntries();
    }

    public void SaveSettings(IEnumerable<ConfigEntry> entries)
    {
        _settingsRepository.SaveSettings(entries);
    }

    public List<AbilityEditorRow> LoadAbilities()
    {
        return _abilitiesRepository.LoadAbilities();
    }

    public void SaveAbilities(IEnumerable<AbilityEditorRow> rows)
    {
        _abilitiesRepository.SaveAbilities(rows);
    }

    public List<AbilityEditorRow> LoadAbilityProfile(ShardType shard, string characterName, string className)
    {
        return _abilityProfileRepository.LoadAbilityProfile(shard, characterName, className);
    }

    public void SaveAbilityProfile(
        ShardType shard,
        string characterName,
        string className,
        IEnumerable<AbilityEditorRow> rows)
    {
        _abilityProfileRepository.SaveAbilityProfile(shard, characterName, className, rows);
    }

    public List<AbilityProfileOverride> LoadAbilityProfileOverrides()
    {
        return _abilityProfileRepository.LoadAbilityProfileOverrides();
    }

    public void DeleteAllAbilityProfiles()
    {
        _abilityProfileRepository.DeleteAllAbilityProfiles();
    }

    public void RestoreAbilityProfileOverrides(IEnumerable<AbilityProfileOverride> values)
    {
        _abilityProfileRepository.RestoreAbilityProfileOverrides(values);
    }

    public CharacterStatsSnapshot? LoadCharacterStats(ShardType shard, string characterName)
    {
        return _characterStatsRepository.LoadCharacterStats(shard, characterName);
    }

    public void SaveCharacterStats(CharacterStatsSnapshot stats)
    {
        _characterStatsRepository.SaveCharacterStats(stats);
    }

    public TargetProfile? Load(ShardType shardType, string targetName)
    {
        return _targetProfileCache.Load(shardType, targetName);
    }

    public void Save(ShardType shardType, TargetProfile profile)
    {
        _targetProfileCache.Save(shardType, profile);
    }

    private List<CharacterStatsSnapshot> LoadAllCharacterStats()
    {
        return _characterStatsRepository.LoadAllCharacterStats();
    }

    public bool IsEmpty()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT 
              (SELECT COUNT(*) FROM settings) AS settings_count,
              (SELECT COUNT(*) FROM abilities) AS abilities_count;
            """;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return true;
        }

        var settingsCount = reader.GetInt32(0);
        var abilitiesCount = reader.GetInt32(1);
        return settingsCount == 0 && abilitiesCount == 0;
    }

    public IReadOnlyDictionary<string, CatalogEntryOverride> LoadCatalogEntryOverrides()
    {
        return _catalogOverrideRepository.LoadCatalogEntryOverrides();
    }

    public void SaveCatalogEntryOverride(CatalogEntryOverride entryOverride)
    {
        _catalogOverrideRepository.SaveCatalogEntryOverride(entryOverride);
    }

    public void DeleteCatalogEntryOverride(string entryKey)
    {
        _catalogOverrideRepository.DeleteCatalogEntryOverride(entryKey);
    }

    public void ExportJson(string outputPath)
    {
        var payload = new BackupPayload
        {
            BackupFormatVersion = CurrentBackupFormatVersion,
            DatabaseMigrationVersion = GetDatabaseMigrationVersion(),
            ExportedUtc = DateTimeOffset.UtcNow,
            SecretsExcluded = true,
            Settings = LoadConfigEntries().Where(x => !SqliteSettingsRepository.IsSensitiveKey(x.Key)).ToList(),
            Abilities = LoadAbilities(),
            CatalogEntryOverrides = LoadCatalogEntryOverrides().Values.ToList(),
            AbilityProfileOverrides = LoadAbilityProfileOverrides(),
            CharacterStats = LoadAllCharacterStats()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void ImportJson(string inputPath, bool replaceExisting = true)
    {
        var json = File.ReadAllText(inputPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Selected backup file is empty.");
        }

        BackupPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<BackupPayload>(json) ?? new BackupPayload();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Selected backup file is not valid HeraldHelper JSON.", ex);
        }

        var backupFormatVersion = payload.ResolvedBackupFormatVersion;
        var databaseMigrationVersion = payload.ResolvedDatabaseMigrationVersion;

        if (backupFormatVersion > CurrentBackupFormatVersion)
        {
            throw new InvalidOperationException(
                $"Backup format version {backupFormatVersion} is newer than the supported version {CurrentBackupFormatVersion}.");
        }

        if (databaseMigrationVersion > CurrentDatabaseMigrationVersion)
        {
            throw new InvalidOperationException(
                $"Database migration version {databaseMigrationVersion} is newer than the supported version {CurrentDatabaseMigrationVersion}.");
        }

        if (replaceExisting)
        {
            var restoredSettings = payload.Settings;
            if (payload.SecretsExcluded)
            {
                restoredSettings = LoadConfigEntries()
                    .Where(x => SqliteSettingsRepository.IsSensitiveKey(x.Key))
                    .Concat(payload.Settings.Where(x => !SqliteSettingsRepository.IsSensitiveKey(x.Key)))
                    .ToList();
            }
            SaveSettings(restoredSettings);
            SaveAbilities(payload.Abilities);
            if (payload.CatalogEntryOverrides is not null)
            {
                DeleteAllCatalogEntryOverrides();
                foreach (var entryOverride in payload.CatalogEntryOverrides)
                {
                    SaveCatalogEntryOverride(entryOverride);
                }
            }

            if (payload.AbilityProfileOverrides is not null)
            {
                DeleteAllAbilityProfiles();
                RestoreAbilityProfileOverrides(payload.AbilityProfileOverrides);
            }
            else if (payload.AbilityProfiles is not null)
            {
                DeleteAllAbilityProfiles();
                RestoreLegacyAbilityProfiles(payload.AbilityProfiles);
            }

            if (payload.CharacterStats is not null)
            {
                DeleteAllCharacterStats();
                foreach (var stats in payload.CharacterStats)
                {
                    SaveCharacterStats(stats);
                }
            }

            return;
        }

        var mergedSettings = LoadConfigEntries()
            .Concat(payload.Settings)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
        SaveSettings(mergedSettings);

        var mergedAbilities = LoadAbilities()
            .Concat(payload.Abilities)
            .ToList();
        SaveAbilities(mergedAbilities);

        if (payload.CatalogEntryOverrides is not null)
        {
            foreach (var entryOverride in payload.CatalogEntryOverrides)
            {
                SaveCatalogEntryOverride(entryOverride);
            }
        }

        if (payload.AbilityProfileOverrides is not null)
        {
            RestoreAbilityProfileOverrides(payload.AbilityProfileOverrides);
        }
        else if (payload.AbilityProfiles is not null)
        {
            RestoreLegacyAbilityProfiles(payload.AbilityProfiles);
        }

        if (payload.CharacterStats is not null)
        {
            foreach (var stats in payload.CharacterStats)
            {
                SaveCharacterStats(stats);
            }
        }
    }

    private void DeleteAllCatalogEntryOverrides()
    {
        _catalogOverrideRepository.DeleteAllCatalogEntryOverrides();
    }

    private void RestoreLegacyAbilityProfiles(IEnumerable<AbilityEditorRow> rows)
    {
        foreach (var group in rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Server) && !string.IsNullOrWhiteSpace(x.ClassName))
            .GroupBy(x => $"{x.Server}|{x.ClassName}", StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (Enum.TryParse<ShardType>(first.Server, true, out var shard))
            {
                SaveAbilityProfile(shard, first.CharacterName, first.ClassName, group);
            }
        }
    }

    private void DeleteAllCharacterStats()
    {
        _characterStatsRepository.DeleteAllCharacterStats();
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

    private sealed class BackupPayload
    {
        [JsonPropertyName("backupFormatVersion")]
        public int BackupFormatVersion { get; set; }

        [JsonPropertyName("databaseMigrationVersion")]
        public int DatabaseMigrationVersion { get; set; }

        [JsonPropertyName("schemaVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SchemaVersion { get; set; }

        public DateTimeOffset ExportedUtc { get; set; }
        public bool SecretsExcluded { get; set; }
        public List<ConfigEntry> Settings { get; set; } = [];
        public List<AbilityEditorRow> Abilities { get; set; } = [];
        public List<CatalogEntryOverride>? CatalogEntryOverrides { get; set; }
        public List<AbilityProfileOverride>? AbilityProfileOverrides { get; set; }
        public List<CharacterStatsSnapshot>? CharacterStats { get; set; }
        // Kept for importing schema-4 backups.
        public List<AbilityEditorRow>? AbilityProfiles { get; set; }

        public int ResolvedBackupFormatVersion =>
            BackupFormatVersion > 0
                ? BackupFormatVersion
                : SchemaVersion ?? 0;

        public int ResolvedDatabaseMigrationVersion =>
            DatabaseMigrationVersion > 0
                ? DatabaseMigrationVersion
                : SchemaVersion ?? CurrentDatabaseMigrationVersion;
    }
}
