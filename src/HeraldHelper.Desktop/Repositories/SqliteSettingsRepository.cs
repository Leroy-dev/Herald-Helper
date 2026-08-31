using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class SqliteSettingsRepository : SqliteRepositoryBase, ISettingsRepository
{
    public SqliteSettingsRepository(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public Dictionary<string, string> LoadSettingsMap()
    {
        using var connection = ConnectionFactory.OpenConnection();
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
        using var connection = ConnectionFactory.OpenConnection();
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

    internal static bool IsSensitiveKey(string key)
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
}
