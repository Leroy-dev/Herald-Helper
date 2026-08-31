using System.Text.Json;
using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Tests;

public sealed class CatalogAndMigrationIntegrityTests
{
    [Fact]
    public void ShippedCatalogs_HaveExpectedClassesAndLocalAssets()
    {
        var root = FindProjectRoot();
        var edenRoot = Path.Combine(root, "data", "eden-charplan");
        var blackthornRoot = Path.Combine(root, "data", "blackthorn-charplan");

        using var edenManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(edenRoot, "generated", "manifest.json")));
        using var blackthornManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(blackthornRoot, "generated", "manifest.json")));
        using var edenIndex = JsonDocument.Parse(File.ReadAllText(Path.Combine(edenRoot, "generated", "classes", "index.json")));
        using var blackthornIndex = JsonDocument.Parse(File.ReadAllText(Path.Combine(blackthornRoot, "generated", "classes", "index.json")));

        Assert.Equal(45, edenManifest.RootElement.GetProperty("classCount").GetInt32());
        Assert.Equal(45, edenIndex.RootElement.GetArrayLength());
        Assert.Contains(edenManifest.RootElement.GetProperty("ignoredClasses").EnumerateArray(),
            x => string.Equals(x.GetString(), "Mauler", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(edenIndex.RootElement.EnumerateArray(),
            x => string.Equals(x.GetProperty("name").GetString(), "Mauler", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(39, blackthornManifest.RootElement.GetProperty("classCount").GetInt32());
        Assert.Equal(39, blackthornIndex.RootElement.GetArrayLength());
        Assert.Equal(
            blackthornManifest.RootElement.GetProperty("iconAssetCount").GetInt32(),
            Directory.EnumerateFiles(Path.Combine(blackthornRoot, "assets", "icons"), "*", SearchOption.AllDirectories).Count());

        AssertClassFilesExist(edenRoot, edenIndex.RootElement);
        AssertClassFilesExist(blackthornRoot, blackthornIndex.RootElement);
    }

    [Fact]
    public void BrowserCatalogs_LoadAllClassesAndApplyLocalOverrides()
    {
        var eden = EdenDataBrowserCatalog.Load();
        var blackthorn = BlackthornDataBrowserCatalog.Load();

        Assert.True(eden.Count > 5_000, $"Only {eden.Count} Eden entries were loaded.");
        Assert.True(blackthorn.Count > 3_000, $"Only {blackthorn.Count} Blackthorn entries were loaded.");
        Assert.DoesNotContain(eden, x => string.Equals(x.ClassName, "Mauler", StringComparison.OrdinalIgnoreCase));

        var original = eden.First(x => x.CastTimeSeconds is > 0);
        var replacement = new CatalogEntryOverride(
            original.EntryKey,
            original.Name + " Test",
            original.EntryType,
            original.ClassName,
            original.Category,
            original.Level,
            9.5,
            original.Summary,
            original.Details,
            original.Icon);
        var overridden = EdenDataBrowserCatalog.Load(
            new Dictionary<string, CatalogEntryOverride>(StringComparer.OrdinalIgnoreCase)
            {
                [original.EntryKey] = replacement
            });

        var updated = Assert.Single(overridden, x => x.EntryKey == original.EntryKey);
        Assert.Equal(9.5, updated.CastTimeSeconds);
        Assert.EndsWith(" Test", updated.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_UpgradesVersionSixDatabaseWithoutLosingExistingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "migration.db");
        Directory.CreateDirectory(directory);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL);
                    CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                    INSERT INTO settings(key, value) VALUES('server', 'Eden');
                    INSERT INTO schema_migrations(version, applied_utc)
                    VALUES (1, 'old'), (2, 'old'), (3, 'old'), (4, 'old'), (5, 'old'), (6, 'old');
                    """;
                command.ExecuteNonQuery();
            }

            var store = new AppDataStore(databasePath);
            store.Initialize();
            store.SaveCharacterStats(new CharacterStatsSnapshot(
                ShardType.Eden, "Rooy", 150, 125, 188, 135, 195, 135, 135, 190, 10, 8, DateTimeOffset.UtcNow));

            Assert.Equal("Eden", store.LoadSettingsMap()["server"]);
            Assert.Equal(188, store.LoadCharacterStats(ShardType.Eden, "Rooy")?.Dexterity);
            using var verification = new SqliteConnection($"Data Source={databasePath}");
            verification.Open();
            using var version = verification.CreateCommand();
            version.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = 8;";
            Assert.Equal(1L, (long)(version.ExecuteScalar() ?? 0L));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void TargetProfileCache_PersistsProfilesSeparatelyPerShard()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "cache.db");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new AppDataStore(databasePath);
            store.Initialize();
            store.Save(ShardType.Eden, new TargetProfile("Teagan", "Eden Guild", "Minstrel", 50, "RR5L0", 11));
            store.Save(ShardType.Blackthorn, new TargetProfile("Teagan", "Blackthorn Guild", "Armsman", 50, "RR6L1", 22));

            Assert.Equal("Eden Guild", store.Load(ShardType.Eden, "teagan")?.Guild);
            Assert.Equal("Blackthorn Guild", store.Load(ShardType.Blackthorn, "TEAGAN")?.Guild);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static void AssertClassFilesExist(string catalogRoot, JsonElement index)
    {
        foreach (var classEntry in index.EnumerateArray())
        {
            var slug = classEntry.GetProperty("slug").GetString();
            Assert.False(string.IsNullOrWhiteSpace(slug));
            Assert.True(
                File.Exists(Path.Combine(catalogRoot, "generated", "classes", slug + ".json")),
                $"Missing generated class file for {slug}.");
        }
    }

    [Fact]
    public void GetDatabaseMigrationVersion_AfterInitialize_ReturnsCurrentVersion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "version.db");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new AppDataStore(databasePath);
            store.Initialize();

            Assert.Equal(AppDataStore.CurrentDatabaseMigrationVersion, store.GetDatabaseMigrationVersion());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ExportJson_IncludesBothVersionFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "export.db");
        var backupPath = Path.Combine(directory, "export.json");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new AppDataStore(databasePath);
            store.Initialize();
            store.ExportJson(backupPath);

            using var document = JsonDocument.Parse(File.ReadAllText(backupPath));
            Assert.True(document.RootElement.TryGetProperty("backupFormatVersion", out var backupFormat));
            Assert.Equal(AppDataStore.CurrentBackupFormatVersion, backupFormat.GetInt32());
            Assert.True(document.RootElement.TryGetProperty("databaseMigrationVersion", out var databaseVersion));
            Assert.Equal(AppDataStore.CurrentDatabaseMigrationVersion, databaseVersion.GetInt32());
            Assert.False(document.RootElement.TryGetProperty("schemaVersion", out _),
                "Legacy schemaVersion should not be written by new exports.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ImportJson_RejectsBackupFormatNewerThanSupported()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "import.db");
        var backupPath = Path.Combine(directory, "future.json");
        Directory.CreateDirectory(directory);

        try
        {
            var payload = new
            {
                backupFormatVersion = 99,
                databaseMigrationVersion = 8,
                Settings = Array.Empty<ConfigEntry>(),
                Abilities = Array.Empty<AbilityEditorRow>(),
                CharacterStats = Array.Empty<CharacterStatsSnapshot>()
            };
            File.WriteAllText(backupPath, JsonSerializer.Serialize(payload));

            var store = new AppDataStore(databasePath);
            store.Initialize();

            var ex = Assert.Throws<InvalidOperationException>(() => store.ImportJson(backupPath));
            Assert.Contains("backup format version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ImportJson_RejectsDatabaseMigrationVersionNewerThanSupported()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "import.db");
        var backupPath = Path.Combine(directory, "future-db.json");
        Directory.CreateDirectory(directory);

        try
        {
            var payload = new
            {
                backupFormatVersion = 1,
                databaseMigrationVersion = 99,
                Settings = Array.Empty<ConfigEntry>(),
                Abilities = Array.Empty<AbilityEditorRow>(),
                CharacterStats = Array.Empty<CharacterStatsSnapshot>()
            };
            File.WriteAllText(backupPath, JsonSerializer.Serialize(payload));

            var store = new AppDataStore(databasePath);
            store.Initialize();

            var ex = Assert.Throws<InvalidOperationException>(() => store.ImportJson(backupPath));
            Assert.Contains("database migration version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void Initialize_UpgradesVersionFourDatabaseWithoutLosingExistingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "v4.db");
        Directory.CreateDirectory(directory);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL);
                    CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                    INSERT INTO settings(key, value) VALUES('server', 'Blackthorn');
                    INSERT INTO schema_migrations(version, applied_utc)
                    VALUES (1, 'old'), (2, 'old'), (3, 'old'), (4, 'old');
                    """;
                command.ExecuteNonQuery();
            }

            var store = new AppDataStore(databasePath);
            store.Initialize();

            Assert.Equal("Blackthorn", store.LoadSettingsMap()["server"]);
            Assert.Equal(AppDataStore.CurrentDatabaseMigrationVersion, store.GetDatabaseMigrationVersion());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeraldHelper.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the HeraldHelper project root.");
    }
}
