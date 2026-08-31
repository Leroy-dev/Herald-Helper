using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace HeraldHelper.Tests;

public sealed class AppDataStoreAbilityProfileTests
{
    [Fact]
    public void ExportJson_ExcludesSecretsAndImportKeepsLocalAuthentication()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(directory, "source.db");
        var targetPath = Path.Combine(directory, "target.db");
        var backupPath = Path.Combine(directory, "backup.json");

        try
        {
            var source = new AppDataStore(sourcePath);
            source.Initialize();
            source.SaveSettings([
                new ConfigEntry { Key = "server", Value = "Eden" },
                new ConfigEntry { Key = "auth.eden.cookieHeader", Value = "secret-cookie" },
                new ConfigEntry { Key = "auth.eden.userAgent", Value = "secret-agent" }
            ]);
            source.ExportJson(backupPath);

            var json = File.ReadAllText(backupPath);
            Assert.DoesNotContain("secret-cookie", json, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-agent", json, StringComparison.Ordinal);
            Assert.Contains("\"SecretsExcluded\": true", json, StringComparison.Ordinal);

            var target = new AppDataStore(targetPath);
            target.Initialize();
            target.SaveSettings([
                new ConfigEntry { Key = "server", Value = "Blackthorn" },
                new ConfigEntry { Key = "auth.eden.cookieHeader", Value = "local-cookie" }
            ]);
            target.ImportJson(backupPath, replaceExisting: true);

            var restored = target.LoadSettingsMap();
            Assert.Equal("Eden", restored["server"]);
            Assert.Equal("local-cookie", restored["auth.eden.cookieHeader"]);
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
    public void SaveAbilityProfile_StoresOnlyCharacterSpecificDeltas()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "profiles.db");

        try
        {
            var store = new AppDataStore(databasePath);
            store.Initialize();
            var rooyProfile = store.LoadAbilityProfile(ShardType.Eden, "Rooy", "Minstrel");
            var cadence = Assert.Single(rooyProfile, x => x.AbilityName == "Commanding Cadence");
            cadence.DurationSeconds = 27;
            cadence.Aliases = "Commandlng Cadence";

            store.SaveAbilityProfile(ShardType.Eden, "Rooy", "Minstrel", rooyProfile);

            var stored = Assert.Single(store.LoadAbilityProfileOverrides());
            Assert.Equal("rooy", stored.CharacterName);
            Assert.Equal("Commanding Cadence", stored.SourceAbilityName);
            Assert.Equal(27, stored.DurationSeconds);
            Assert.Equal("Commandlng Cadence", stored.Aliases);

            var savedCadence = Assert.Single(
                store.LoadAbilityProfile(ShardType.Eden, "Rooy", "Minstrel"),
                x => x.AbilityName == "Commanding Cadence");
            var otherCadence = Assert.Single(
                store.LoadAbilityProfile(ShardType.Eden, "AnotherMinstrel", "Minstrel"),
                x => x.AbilityName == "Commanding Cadence");
            Assert.Equal(27, savedCadence.DurationSeconds);
            Assert.Equal(29, otherCadence.DurationSeconds);
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
    public void CharacterStats_ArePersistedByShardAndCharacter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AppDataStore(Path.Combine(directory, "stats.db"));
            store.Initialize();
            store.SaveCharacterStats(new HeraldHelper.Domain.Models.CharacterStatsSnapshot(
                ShardType.Eden, "Rooy", 150, 125, 188, 135, 195, 135, 135, 190, 10, 8, DateTimeOffset.UtcNow));

            var result = store.LoadCharacterStats(ShardType.Eden, "Rooy");

            Assert.NotNull(result);
            Assert.Equal(188, result!.Dexterity);
            Assert.Equal(10, result.CastingSpeedPercent);
            Assert.Null(store.LoadCharacterStats(ShardType.Blackthorn, "Rooy"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ImportJson_RestoresLegacyProfilesIndependentlyFromCharacterStats()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "legacy.db");
        var backupPath = Path.Combine(directory, "legacy.json");
        Directory.CreateDirectory(directory);

        try
        {
            var legacyCadence = new AbilityEditorRow
            {
                IsEnabled = true,
                AbilityName = "Commanding Cadence",
                SourceAbilityName = "Commanding Cadence",
                SkillCode = "m",
                EffectType = "m",
                SourceEffectType = "m",
                DurationSeconds = 27,
                Server = "Eden",
                ClassName = "Minstrel",
                CharacterName = "Rooy"
            };
            var payload = new
            {
                SchemaVersion = 4,
                Settings = Array.Empty<ConfigEntry>(),
                Abilities = Array.Empty<AbilityEditorRow>(),
                AbilityProfiles = new[] { legacyCadence },
                CharacterStats = Array.Empty<HeraldHelper.Domain.Models.CharacterStatsSnapshot>()
            };
            File.WriteAllText(backupPath, JsonSerializer.Serialize(payload));

            var store = new AppDataStore(databasePath);
            store.Initialize();
            store.ImportJson(backupPath);

            var restored = Assert.Single(
                store.LoadAbilityProfile(ShardType.Eden, "Rooy", "Minstrel"),
                x => x.AbilityName == "Commanding Cadence");
            Assert.Equal(27, restored.DurationSeconds);
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
}
