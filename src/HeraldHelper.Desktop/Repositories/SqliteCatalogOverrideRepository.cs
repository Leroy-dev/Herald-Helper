using HeraldHelper.Domain.Models;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteCatalogOverrideRepository : SqliteRepositoryBase, ICatalogOverrideRepository
{
    public SqliteCatalogOverrideRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public IReadOnlyDictionary<string, CatalogEntryOverride> LoadCatalogEntryOverrides()
    {
        using var connection = ConnectionFactory.OpenConnection();
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
        using var connection = ConnectionFactory.OpenConnection();
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
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM eden_entry_overrides WHERE entry_key = $entry_key;";
        cmd.Parameters.AddWithValue("$entry_key", entryKey);
        cmd.ExecuteNonQuery();
    }

    public void DeleteAllCatalogEntryOverrides()
    {
        using var connection = ConnectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM eden_entry_overrides;";
        cmd.ExecuteNonQuery();
    }
}
