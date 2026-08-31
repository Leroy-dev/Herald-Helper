namespace HeraldHelper.Desktop.Repositories;

public interface ISettingsRepository
{
    Dictionary<string, string> LoadSettingsMap();
    List<ConfigEntry> LoadConfigEntries();
    void SaveSettings(IEnumerable<ConfigEntry> entries);
}
