using System.Collections.ObjectModel;

namespace HeraldHelper.Desktop.Repositories;

public interface ICatalogOverrideRepository
{
    IReadOnlyDictionary<string, CatalogEntryOverride> LoadCatalogEntryOverrides();
    void SaveCatalogEntryOverride(CatalogEntryOverride entryOverride);
    void DeleteCatalogEntryOverride(string entryKey);
    void DeleteAllCatalogEntryOverrides();
}
