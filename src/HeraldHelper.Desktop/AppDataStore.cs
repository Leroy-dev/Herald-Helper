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

public sealed class AppDataStore : ITargetProfileCache
{
    public const int CurrentDatabaseMigrationVersion = 8;
    public const int CurrentBackupFormatVersion = 1;

    private readonly SqliteConnectionFactory _connectionFactory;

    public AppDataStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var connectionString = $"Data Source={databasePath}";
        _connectionFactory = new SqliteConnectionFactory(connectionString);
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
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = DecryptIfNeeded(key, reader.GetString(1));
            map[key] = value;
        }

        return map;
    }

    public List<ConfigEntry> LoadConfigEntries()
    {
        return LoadSettingsMap()
            .Select(x => new ConfigEntry { Key = x.Key, Value = x.Value })
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SaveSettings(IEnumerable<ConfigEntry> entries)
    {
        var list = entries.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList();
        using var connection = _connectionFactory.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM settings;";
            delete.ExecuteNonQuery();
        }

        foreach (var entry in list)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT INTO settings(key, value) VALUES($k, $v);";
            var key = entry.Key.Trim();
            var clearValue = entry.Value?.Trim() ?? string.Empty;
            insert.Parameters.AddWithValue("$k", key);
            insert.Parameters.AddWithValue("$v", EncryptIfNeeded(key, clearValue));
            insert.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public List<AbilityEditorRow> LoadAbilities()
    {
        using var connection = _connectionFactory.OpenConnection();
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

        using var connection = _connectionFactory.OpenConnection();
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

    public List<AbilityEditorRow> LoadAbilityProfile(ShardType shard, string characterName, string className)
    {
        if (shard is not (ShardType.Eden or ShardType.Blackthorn) || string.IsNullOrWhiteSpace(className))
        {
            return [];
        }

        var server = shard.ToString().ToLowerInvariant();
        var normalizedCharacter = NormalizeProfileSegment(characterName);
        var baseRows = AbilityProfileCatalog.GetProfile(shard, className)
            .Select(x => new AbilityEditorRow
            {
                IsEnabled = true,
                AbilityName = x.Name,
                SkillCode = x.SkillCode,
                DurationSeconds = x.DurationSeconds,
                EffectType = x.SkillCode,
                Category = x.Category,
                Level = x.Level,
                Server = server,
                ClassName = x.ClassName,
                CharacterName = normalizedCharacter,
                SourceAbilityName = x.Name,
                SourceEffectType = x.SkillCode,
                IsCustom = false
            })
            .ToDictionary(SourceProfileEntryKey, StringComparer.OrdinalIgnoreCase);

        var overrides = new List<AbilityProfileOverride>();
        if (!string.IsNullOrWhiteSpace(normalizedCharacter))
        {
            // Keep pre-character profiles active until the user saves this character profile.
            overrides.AddRange(LoadAbilityProfileOverrides(server, className, string.Empty));
        }
        overrides.AddRange(LoadAbilityProfileOverrides(server, className, normalizedCharacter));
        foreach (var entryOverride in overrides)
        {
            var key = SourceProfileEntryKey(entryOverride.SourceAbilityName, entryOverride.SourceEffectType);
            if (entryOverride.IsCustom)
            {
                baseRows[key] = CreateCustomProfileRow(entryOverride);
                continue;
            }

            if (!baseRows.TryGetValue(key, out var row))
            {
                continue;
            }

            row.IsEnabled = entryOverride.IsEnabled ?? row.IsEnabled;
            row.AbilityName = entryOverride.AbilityName ?? row.AbilityName;
            row.SkillCode = entryOverride.SkillCode ?? row.SkillCode;
            row.DurationSeconds = entryOverride.DurationSeconds ?? row.DurationSeconds;
            row.EffectType = entryOverride.EffectType ?? row.EffectType;
            row.Category = entryOverride.Category ?? row.Category;
            if (entryOverride.HasLevelOverride)
            {
                row.Level = entryOverride.Level;
            }

            row.Aliases = entryOverride.Aliases ?? row.Aliases;
        }

        return baseRows.Values
            .OrderBy(x => x.Level ?? int.MaxValue)
            .ThenBy(x => x.AbilityName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SaveAbilityProfile(
        ShardType shard,
        string characterName,
        string className,
        IEnumerable<AbilityEditorRow> rows)
    {
        var server = shard.ToString().ToLowerInvariant();
        var normalizedClass = className.Trim();
        var normalizedCharacter = NormalizeProfileSegment(characterName);
        var baseRows = AbilityProfileCatalog.GetProfile(shard, normalizedClass)
            .ToDictionary(
                x => SourceProfileEntryKey(x.Name, x.SkillCode),
                StringComparer.OrdinalIgnoreCase);
        var deltas = new List<AbilityProfileOverride>();

        foreach (var row in rows.Where(x => !string.IsNullOrWhiteSpace(x.AbilityName)))
        {
            var sourceName = string.IsNullOrWhiteSpace(row.SourceAbilityName)
                ? row.AbilityName.Trim()
                : row.SourceAbilityName.Trim();
            var sourceEffect = NormalizeCode(
                string.IsNullOrWhiteSpace(row.SourceEffectType) ? row.EffectType : row.SourceEffectType,
                "s");
            var sourceKey = SourceProfileEntryKey(sourceName, sourceEffect);
            if (row.IsCustom || !baseRows.TryGetValue(sourceKey, out var baseRow))
            {
                deltas.Add(new AbilityProfileOverride(
                    server,
                    normalizedClass,
                    normalizedCharacter,
                    sourceName,
                    sourceEffect,
                    row.IsEnabled,
                    row.AbilityName.Trim(),
                    NormalizeCode(row.SkillCode, "s"),
                    Math.Max(1, row.DurationSeconds),
                    NormalizeCode(row.EffectType, "s"),
                    row.Category?.Trim() ?? string.Empty,
                    row.Level,
                    true,
                    NormalizeAliases(row.Aliases),
                    true));
                continue;
            }

            var delta = new AbilityProfileOverride(
                server,
                normalizedClass,
                normalizedCharacter,
                sourceName,
                sourceEffect,
                row.IsEnabled == true ? null : false,
                SameText(row.AbilityName, baseRow.Name) ? null : row.AbilityName.Trim(),
                SameText(NormalizeCode(row.SkillCode, "s"), baseRow.SkillCode) ? null : NormalizeCode(row.SkillCode, "s"),
                row.DurationSeconds == baseRow.DurationSeconds ? null : Math.Max(1, row.DurationSeconds),
                SameText(NormalizeCode(row.EffectType, "s"), baseRow.SkillCode) ? null : NormalizeCode(row.EffectType, "s"),
                SameText(row.Category, baseRow.Category) ? null : row.Category?.Trim() ?? string.Empty,
                row.Level == baseRow.Level ? null : row.Level,
                row.Level != baseRow.Level,
                string.IsNullOrWhiteSpace(row.Aliases) ? null : NormalizeAliases(row.Aliases),
                false);
            if (HasProfileDelta(delta))
            {
                deltas.Add(delta);
            }
        }

        using var connection = _connectionFactory.OpenConnection();
        using var tx = connection.BeginTransaction();
        DeleteAbilityProfileOverrides(connection, tx, server, normalizedClass, normalizedCharacter);
        if (!string.IsNullOrWhiteSpace(normalizedCharacter))
        {
            // A saved character profile supersedes the legacy shard-wide profile.
            DeleteAbilityProfileOverrides(connection, tx, server, normalizedClass, string.Empty);
        }

        foreach (var delta in deltas)
        {
            InsertAbilityProfileOverride(connection, tx, delta);
        }

        tx.Commit();
    }

    public List<AbilityProfileOverride> LoadAbilityProfileOverrides()
    {
        return LoadAbilityProfileOverrides(null, null, null);
    }

    public CharacterStatsSnapshot? LoadCharacterStats(ShardType shard, string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }
        using var connection = _connectionFactory.OpenConnection();
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
            DateTimeOffset.Parse(reader.GetString(10), System.Globalization.CultureInfo.InvariantCulture));
    }

    public void SaveCharacterStats(CharacterStatsSnapshot stats)
    {
        using var connection = _connectionFactory.OpenConnection();
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

    public TargetProfile? Load(ShardType shardType, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        using var connection = _connectionFactory.OpenConnection();
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

    public void Save(ShardType shardType, TargetProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return;
        }

        using var connection = _connectionFactory.OpenConnection();
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

    private List<CharacterStatsSnapshot> LoadAllCharacterStats()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT server, character_name, strength, constitution, dexterity, quickness,
                   intelligence, piety, empathy, charisma, casting_speed_percent,
                   spell_damage_percent, updated_utc
            FROM character_stats;
            """;
        var result = new List<CharacterStatsSnapshot>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!Enum.TryParse<ShardType>(reader.GetString(0), true, out var shard))
            {
                continue;
            }
            result.Add(new CharacterStatsSnapshot(
                shard, reader.GetString(1), ReadNullableInt(reader, 2), ReadNullableInt(reader, 3),
                ReadNullableInt(reader, 4), ReadNullableInt(reader, 5), ReadNullableInt(reader, 6),
                ReadNullableInt(reader, 7), ReadNullableInt(reader, 8), ReadNullableInt(reader, 9),
                reader.GetDouble(10), reader.GetDouble(11),
                DateTimeOffset.Parse(reader.GetString(12), System.Globalization.CultureInfo.InvariantCulture)));
        }
        return result;
    }

    private static int? ReadNullableInt(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddNullableInt(SqliteCommand command, string name, int? value)
    {
        command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value.Value);
    }

    private static void AddNullableString(SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
    }

    private List<AbilityProfileOverride> LoadAbilityProfileOverrides(
        string? server,
        string? className,
        string? characterName)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT server, class_name, character_name, source_ability_name, source_effect_type,
                   enabled_override, ability_name_override, skill_code_override,
                   duration_seconds_override, effect_type_override, category_override,
                   level_override, has_level_override, aliases_override, is_custom
            FROM ability_profile_overrides
            WHERE ($server IS NULL OR server = $server)
              AND ($class_name IS NULL OR class_name = $class_name)
              AND ($character_name IS NULL OR character_name = $character_name)
            ORDER BY server, class_name, character_name, source_ability_name;
            """;
        cmd.Parameters.AddWithValue("$server", (object?)server ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$class_name", (object?)className ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$character_name", (object?)characterName ?? DBNull.Value);

        var result = new List<AbilityProfileOverride>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AbilityProfileOverride(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5) != 0,
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.GetInt32(12) != 0,
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.GetInt32(14) != 0));
        }

        return result;
    }

    private static AbilityEditorRow CreateCustomProfileRow(AbilityProfileOverride entryOverride)
    {
        return new AbilityEditorRow
        {
            IsEnabled = entryOverride.IsEnabled ?? true,
            AbilityName = entryOverride.AbilityName ?? entryOverride.SourceAbilityName,
            SkillCode = entryOverride.SkillCode ?? entryOverride.SourceEffectType,
            DurationSeconds = Math.Max(1, entryOverride.DurationSeconds ?? 1),
            EffectType = entryOverride.EffectType ?? entryOverride.SourceEffectType,
            Category = entryOverride.Category ?? "Custom",
            Level = entryOverride.Level,
            Server = entryOverride.Server,
            ClassName = entryOverride.ClassName,
            CharacterName = entryOverride.CharacterName,
            SourceAbilityName = entryOverride.SourceAbilityName,
            SourceEffectType = entryOverride.SourceEffectType,
            Aliases = entryOverride.Aliases ?? string.Empty,
            IsCustom = true
        };
    }

    private static bool HasProfileDelta(AbilityProfileOverride value)
    {
        return value.IsEnabled is not null || value.AbilityName is not null || value.SkillCode is not null ||
               value.DurationSeconds is not null || value.EffectType is not null || value.Category is not null ||
               value.HasLevelOverride || value.Aliases is not null || value.IsCustom;
    }

    private static string SourceProfileEntryKey(AbilityEditorRow row)
    {
        return SourceProfileEntryKey(row.SourceAbilityName, row.SourceEffectType);
    }

    private static string SourceProfileEntryKey(string abilityName, string effectType)
    {
        return $"{abilityName.Trim()}|{NormalizeCode(effectType, "s")}";
    }

    private static string NormalizeProfileSegment(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string? NormalizeAliases(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join("; ", value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static bool SameText(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteAbilityProfileOverrides(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string server,
        string className,
        string characterName)
    {
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = """
            DELETE FROM ability_profile_overrides
            WHERE server = $server AND class_name = $class_name AND character_name = $character_name;
            """;
        delete.Parameters.AddWithValue("$server", server);
        delete.Parameters.AddWithValue("$class_name", className);
        delete.Parameters.AddWithValue("$character_name", characterName);
        delete.ExecuteNonQuery();
    }

    private static void InsertAbilityProfileOverride(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AbilityProfileOverride value)
    {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT OR REPLACE INTO ability_profile_overrides(
                server, class_name, character_name, source_ability_name, source_effect_type,
                enabled_override, ability_name_override, skill_code_override,
                duration_seconds_override, effect_type_override, category_override,
                level_override, has_level_override, aliases_override, is_custom)
            VALUES($server, $class_name, $character_name, $source_name, $source_effect,
                   $enabled, $name, $skill, $duration, $effect, $category,
                   $level, $has_level, $aliases, $custom);
            """;
        insert.Parameters.AddWithValue("$server", value.Server);
        insert.Parameters.AddWithValue("$class_name", value.ClassName);
        insert.Parameters.AddWithValue("$character_name", value.CharacterName);
        insert.Parameters.AddWithValue("$source_name", value.SourceAbilityName);
        insert.Parameters.AddWithValue("$source_effect", value.SourceEffectType);
        insert.Parameters.AddWithValue("$enabled", (object?)value.IsEnabled is null ? DBNull.Value : value.IsEnabled.Value ? 1 : 0);
        insert.Parameters.AddWithValue("$name", (object?)value.AbilityName ?? DBNull.Value);
        insert.Parameters.AddWithValue("$skill", (object?)value.SkillCode ?? DBNull.Value);
        insert.Parameters.AddWithValue("$duration", (object?)value.DurationSeconds ?? DBNull.Value);
        insert.Parameters.AddWithValue("$effect", (object?)value.EffectType ?? DBNull.Value);
        insert.Parameters.AddWithValue("$category", (object?)value.Category ?? DBNull.Value);
        insert.Parameters.AddWithValue("$level", (object?)value.Level ?? DBNull.Value);
        insert.Parameters.AddWithValue("$has_level", value.HasLevelOverride ? 1 : 0);
        insert.Parameters.AddWithValue("$aliases", (object?)value.Aliases ?? DBNull.Value);
        insert.Parameters.AddWithValue("$custom", value.IsCustom ? 1 : 0);
        insert.ExecuteNonQuery();
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
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                entry_key,
                name,
                entry_type,
                class_name,
                category,
                level,
                cast_time_seconds,
                summary,
                details,
                icon_sprite_sheet,
                icon_x,
                icon_y,
                icon_width,
                icon_height,
                icon_border_index,
                icon_spell_badge_index,
                icon_corner_up_left_index,
                icon_corner_up_index,
                icon_corner_up_right_index,
                icon_corner_right_index,
                icon_corner_down_right_index,
                icon_corner_down_index,
                icon_corner_left_index
            FROM eden_entry_overrides;
            """;

        var result = new Dictionary<string, CatalogEntryOverride>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            IconSpriteRef? icon = null;
            if (!reader.IsDBNull(9))
            {
                icon = new IconSpriteRef(
                    reader.GetString(9),
                    ReadIntOrZero(reader, 10),
                    ReadIntOrZero(reader, 11),
                    ReadIntOrZero(reader, 12),
                    ReadIntOrZero(reader, 13),
                    ReadIntOrZero(reader, 14),
                    ReadIntOrZero(reader, 15),
                    ReadIntOrZero(reader, 16),
                    ReadIntOrZero(reader, 17),
                    ReadIntOrZero(reader, 18),
                    ReadIntOrZero(reader, 19),
                    ReadIntOrZero(reader, 20),
                    ReadIntOrZero(reader, 21),
                    ReadIntOrZero(reader, 22));
            }

            result[reader.GetString(0)] = new CatalogEntryOverride(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetDouble(6),
                reader.GetString(7),
                reader.GetString(8),
                icon);
        }

        return result;
    }

    public void SaveCatalogEntryOverride(CatalogEntryOverride entryOverride)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO eden_entry_overrides(
                entry_key,
                name,
                entry_type,
                class_name,
                category,
                level,
                cast_time_seconds,
                summary,
                details,
                icon_sprite_sheet,
                icon_x,
                icon_y,
                icon_width,
                icon_height,
                icon_border_index,
                icon_spell_badge_index,
                icon_corner_up_left_index,
                icon_corner_up_index,
                icon_corner_up_right_index,
                icon_corner_right_index,
                icon_corner_down_right_index,
                icon_corner_down_index,
                icon_corner_left_index)
            VALUES(
                $entry_key,
                $name,
                $entry_type,
                $class_name,
                $category,
                $level,
                $cast_time_seconds,
                $summary,
                $details,
                $icon_sprite_sheet,
                $icon_x,
                $icon_y,
                $icon_width,
                $icon_height,
                $icon_border_index,
                $icon_spell_badge_index,
                $icon_corner_up_left_index,
                $icon_corner_up_index,
                $icon_corner_up_right_index,
                $icon_corner_right_index,
                $icon_corner_down_right_index,
                $icon_corner_down_index,
                $icon_corner_left_index)
            ON CONFLICT(entry_key) DO UPDATE SET
                name = excluded.name,
                entry_type = excluded.entry_type,
                class_name = excluded.class_name,
                category = excluded.category,
                level = excluded.level,
                cast_time_seconds = excluded.cast_time_seconds,
                summary = excluded.summary,
                details = excluded.details,
                icon_sprite_sheet = excluded.icon_sprite_sheet,
                icon_x = excluded.icon_x,
                icon_y = excluded.icon_y,
                icon_width = excluded.icon_width,
                icon_height = excluded.icon_height,
                icon_border_index = excluded.icon_border_index,
                icon_spell_badge_index = excluded.icon_spell_badge_index,
                icon_corner_up_left_index = excluded.icon_corner_up_left_index,
                icon_corner_up_index = excluded.icon_corner_up_index,
                icon_corner_up_right_index = excluded.icon_corner_up_right_index,
                icon_corner_right_index = excluded.icon_corner_right_index,
                icon_corner_down_right_index = excluded.icon_corner_down_right_index,
                icon_corner_down_index = excluded.icon_corner_down_index,
                icon_corner_left_index = excluded.icon_corner_left_index;
            """;
        cmd.Parameters.AddWithValue("$entry_key", entryOverride.EntryKey);
        cmd.Parameters.AddWithValue("$name", entryOverride.Name);
        cmd.Parameters.AddWithValue("$entry_type", entryOverride.EntryType);
        cmd.Parameters.AddWithValue("$class_name", entryOverride.ClassName);
        cmd.Parameters.AddWithValue("$category", entryOverride.Category);
        cmd.Parameters.AddWithValue("$level", (object?)entryOverride.Level ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cast_time_seconds", (object?)entryOverride.CastTimeSeconds ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$summary", entryOverride.Summary);
        cmd.Parameters.AddWithValue("$details", entryOverride.Details);
        cmd.Parameters.AddWithValue("$icon_sprite_sheet", (object?)entryOverride.Icon?.SpriteSheet ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$icon_x", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.X);
        cmd.Parameters.AddWithValue("$icon_y", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.Y);
        cmd.Parameters.AddWithValue("$icon_width", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.Width);
        cmd.Parameters.AddWithValue("$icon_height", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.Height);
        cmd.Parameters.AddWithValue("$icon_border_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.BorderIndex);
        cmd.Parameters.AddWithValue("$icon_spell_badge_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.SpellBadgeIndex);
        cmd.Parameters.AddWithValue("$icon_corner_up_left_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.UpLeftCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_up_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.UpCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_up_right_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.UpRightCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_right_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.RightCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_down_right_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.DownRightCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_down_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.DownCornerIndex);
        cmd.Parameters.AddWithValue("$icon_corner_left_index", entryOverride.Icon is null ? DBNull.Value : entryOverride.Icon.LeftCornerIndex);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCatalogEntryOverride(string entryKey)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM eden_entry_overrides WHERE entry_key = $entry_key;";
        cmd.Parameters.AddWithValue("$entry_key", entryKey);
        cmd.ExecuteNonQuery();
    }

    private static string NormalizeCode(string value, string fallback)
    {
        var v = value.Trim().ToLowerInvariant();
        return v is "m" or "s" or "r" ? v : fallback;
    }

    private static int ReadIntOrZero(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    public void ExportJson(string outputPath)
    {
        var payload = new BackupPayload
        {
            BackupFormatVersion = CurrentBackupFormatVersion,
            DatabaseMigrationVersion = GetDatabaseMigrationVersion(),
            ExportedUtc = DateTimeOffset.UtcNow,
            SecretsExcluded = true,
            Settings = LoadConfigEntries().Where(x => !IsSensitiveKey(x.Key)).ToList(),
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
                    .Where(x => IsSensitiveKey(x.Key))
                    .Concat(payload.Settings.Where(x => !IsSensitiveKey(x.Key)))
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
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM eden_entry_overrides;";
        cmd.ExecuteNonQuery();
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

    private void RestoreAbilityProfileOverrides(IEnumerable<AbilityProfileOverride> values)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var tx = connection.BeginTransaction();
        foreach (var value in values)
        {
            InsertAbilityProfileOverride(connection, tx, value);
        }

        tx.Commit();
    }

    private void DeleteAllAbilityProfiles()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ability_profile_entries; DELETE FROM ability_profile_overrides;";
        cmd.ExecuteNonQuery();
    }

    private void DeleteAllCharacterStats()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM character_stats;";
        cmd.ExecuteNonQuery();
    }

    private static bool IsSensitiveKey(string key)
    {
        var k = key.ToLowerInvariant();
        return k.Contains("cookie") || k.Contains("token") || k.Contains("sid") || k.Contains("useragent") || k.Contains("powsess");
    }

    private static string EncryptIfNeeded(string key, string value)
    {
        if (!IsSensitiveKey(key) || string.IsNullOrEmpty(value))
        {
            return value;
        }

        var raw = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        return "enc:" + Convert.ToBase64String(protectedBytes);
    }

    private static string DecryptIfNeeded(string key, string storedValue)
    {
        if (!IsSensitiveKey(key) || !storedValue.StartsWith("enc:", StringComparison.Ordinal))
        {
            return storedValue;
        }

        try
        {
            var cipher = Convert.FromBase64String(storedValue[4..]);
            var clear = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        catch
        {
            return storedValue;
        }
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
