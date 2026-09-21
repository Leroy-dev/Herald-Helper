using HeraldHelper.Desktop;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Desktop.Services;

namespace HeraldHelper.Tests;

public sealed class LegacySettingsMigratorTests
{
    [Fact]
    public void Migrate_ShardWideClassKey_BecomesDefaultKey()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["ability.profile.class.eden"] = "Minstrel",
            ["unrelated"] = "kept"
        });

        Assert.Equal(1, LegacySettingsMigrator.Migrate(repo));
        Assert.False(repo.Map.ContainsKey("ability.profile.class.eden"));
        Assert.Equal("Minstrel", repo.Map["ability.profile.class.eden.default"]);
        Assert.Equal("kept", repo.Map["unrelated"]);
    }

    [Fact]
    public void Migrate_UnnormalizedOcrWindowsSegment_IsRenamed()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["daoc.ocr.windows.eden.ro'y"] = "[{\"Key\":\"ChatWindow1\"}]"
        });

        Assert.Equal(1, LegacySettingsMigrator.Migrate(repo));
        Assert.False(repo.Map.ContainsKey("daoc.ocr.windows.eden.ro'y"));
        Assert.Equal("[{\"Key\":\"ChatWindow1\"}]", repo.Map["daoc.ocr.windows.eden.ro_y"]);
    }

    [Fact]
    public void Migrate_ExistingNormalizedKeyWins_LegacyDropped()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["daoc.ocr.windows.eden.ro'y"] = "old",
            ["daoc.ocr.windows.eden.ro_y"] = "new"
        });

        Assert.Equal(1, LegacySettingsMigrator.Migrate(repo));
        Assert.Equal("new", repo.Map["daoc.ocr.windows.eden.ro_y"]);
    }

    [Fact]
    public void Migrate_EmptyLegacyValue_IsDroppedWithoutRename()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["ability.profile.class.eden"] = ""
        });

        Assert.Equal(1, LegacySettingsMigrator.Migrate(repo));
        Assert.False(repo.Map.ContainsKey("ability.profile.class.eden"));
        Assert.False(repo.Map.ContainsKey("ability.profile.class.eden.default"));
    }

    [Fact]
    public void Migrate_NoLegacyKeys_IsNoOp()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["daoc.ocr.windows.eden.rooy"] = "x",
            ["ability.profile.class.eden.rooy"] = "Minstrel"
        });

        Assert.Equal(0, LegacySettingsMigrator.Migrate(repo));
        Assert.Equal(2, repo.Map.Count);
        Assert.False(repo.SaveCalled);
    }

    [Fact]
    public void Migrate_IsIdempotent()
    {
        var repo = new MapSettingsRepository(new Dictionary<string, string>
        {
            ["ability.profile.class.eden"] = "Minstrel",
            ["daoc.ocr.windows.eden.ro'y"] = "v"
        });

        Assert.Equal(2, LegacySettingsMigrator.Migrate(repo));
        Assert.Equal(0, LegacySettingsMigrator.Migrate(repo));
    }

    private sealed class MapSettingsRepository : ISettingsRepository
    {
        public MapSettingsRepository(Dictionary<string, string> initial)
        {
            Map = new Dictionary<string, string>(initial, StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, string> Map { get; }
        public bool SaveCalled { get; private set; }

        public Dictionary<string, string> LoadSettingsMap()
        {
            return new Dictionary<string, string>(Map, StringComparer.OrdinalIgnoreCase);
        }

        public List<ConfigEntry> LoadConfigEntries()
        {
            return Map.Select(x => new ConfigEntry { Key = x.Key, Value = x.Value }).ToList();
        }

        public void SaveSettings(IEnumerable<ConfigEntry> entries)
        {
            SaveCalled = true;
            Map.Clear();
            foreach (var entry in entries)
            {
                Map[entry.Key] = entry.Value;
            }
        }
    }
}
