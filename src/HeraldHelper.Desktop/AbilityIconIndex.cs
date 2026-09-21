using System.Collections.Concurrent;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

/// <summary>
/// Per-shard charplan catalog cache: the full entry list plus a name → icon
/// index derived from it. Loaded once per shard on first use — catalog parses
/// are ~30 MB of JSON, so both the icon lookup and the ability picker share
/// the one load. Abilities the catalog has no icon for simply render without.
/// </summary>
internal sealed class AbilityIconIndex
{
    private readonly ICatalogOverrideRepository _overrides;
    private readonly ConcurrentDictionary<ShardType, IReadOnlyList<CatalogBrowserEntry>> _entries = new();
    private readonly ConcurrentDictionary<ShardType, IReadOnlyDictionary<string, IconSpriteRef>> _icons = new();

    public AbilityIconIndex(ICatalogOverrideRepository overrides)
    {
        _overrides = overrides;
    }

    public IReadOnlyDictionary<string, IconSpriteRef> Get(ShardType shard)
    {
        return _icons.GetOrAdd(shard, s => BuildIconMap(GetEntries(s)));
    }

    public IReadOnlyList<CatalogBrowserEntry> GetEntries(ShardType shard)
    {
        return _entries.GetOrAdd(shard, LoadEntries);
    }

    public void Invalidate()
    {
        _entries.Clear();
        _icons.Clear();
    }

    private IReadOnlyList<CatalogBrowserEntry> LoadEntries(ShardType shard)
    {
        try
        {
            return shard switch
            {
                ShardType.Eden => EdenDataBrowserCatalog.Load(_overrides.LoadCatalogEntryOverrides()),
                ShardType.Blackthorn => BlackthornDataBrowserCatalog.Load(_overrides.LoadCatalogEntryOverrides()),
                _ => []
            };
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, IconSpriteRef> BuildIconMap(IReadOnlyList<CatalogBrowserEntry> entries)
    {
        return entries
            .Where(x => x.Icon is not null)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Icon!,
                StringComparer.OrdinalIgnoreCase);
    }
}
