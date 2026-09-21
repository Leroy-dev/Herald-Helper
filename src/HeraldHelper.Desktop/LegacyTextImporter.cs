using System.IO;
namespace HeraldHelper.Desktop;

/// <summary>
/// First-run import of AHK-era flat files: cfg.ini (`key:value` lines) and
/// abilities.txt. Runs only while the DB is empty; afterwards the settings
/// table is the source of truth.
/// </summary>
public static class LegacyTextImporter
{
    public static void ImportIfNeeded(AppDataStore store, string cfgPath, string abilitiesPath)
    {
        if (!store.IsEmpty())
        {
            return;
        }

        var configEntries = File.Exists(cfgPath)
            ? LoadCfgEntries(cfgPath)
            : [];

        var abilityEntries = File.Exists(abilitiesPath)
            ? AbilityFileStore.Load(abilitiesPath)
            : [];

        if (configEntries.Count > 0)
        {
            store.SaveSettings(configEntries);
        }

        if (abilityEntries.Count > 0)
        {
            store.SaveAbilities(abilityEntries);
        }
    }

    private static List<ConfigEntry> LoadCfgEntries(string cfgPath)
    {
        return File.ReadLines(cfgPath)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .Select(parts => new ConfigEntry { Key = parts[0].Trim(), Value = parts[1].Trim() })
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
