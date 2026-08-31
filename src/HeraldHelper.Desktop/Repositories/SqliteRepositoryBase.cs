using System.Globalization;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal abstract class SqliteRepositoryBase
{
    protected SqliteConnectionFactory ConnectionFactory { get; }

    protected SqliteRepositoryBase(SqliteConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;
    }

    protected static int? ReadNullableInt(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    protected static string? ReadNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    protected static void AddNullableInt(SqliteCommand command, string name, int? value)
    {
        command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value.Value);
    }

    protected static void AddNullableString(SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value!.Trim());
    }

    protected static string NormalizeProfileSegment(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    protected static string NormalizeCode(string value, string fallback)
    {
        var v = value.Trim().ToLowerInvariant();
        return v is "m" or "s" or "r" ? v : fallback;
    }

    protected static int ReadIntOrZero(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }
}
