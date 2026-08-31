using System.IO;
namespace HeraldHelper.Desktop;

public static class LegacyTextImporter
{
    public static void ImportIfNeeded(AppDataStore store, string cfgPath, string abilitiesPath)
    {
        if (!store.IsEmpty())
        {
            return;
        }

        var configEntries = File.Exists(cfgPath)
            ? CfgIniStore.LoadEntries(cfgPath)
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
}
