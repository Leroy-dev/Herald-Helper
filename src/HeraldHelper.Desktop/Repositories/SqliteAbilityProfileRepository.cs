using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Parsing;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteAbilityProfileRepository : SqliteRepositoryBase, IAbilityProfileRepository
{
    public SqliteAbilityProfileRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
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

        using var connection = ConnectionFactory.OpenConnection();
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

    private List<AbilityProfileOverride> LoadAbilityProfileOverrides(
        string? server,
        string? className,
        string? characterName)
    {
        using var connection = ConnectionFactory.OpenConnection();
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

    public void DeleteAllAbilityProfiles()
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ability_profile_overrides; DELETE FROM ability_profile_entries;";
        cmd.ExecuteNonQuery();
    }

    public void RestoreAbilityProfileOverrides(IEnumerable<AbilityProfileOverride> overrides)
    {
        var list = overrides.ToList();
        using var connection = ConnectionFactory.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM ability_profile_overrides;";
            delete.ExecuteNonQuery();
        }

        foreach (var entry in list)
        {
            InsertAbilityProfileOverride(connection, tx, entry);
        }

        tx.Commit();
    }
}
