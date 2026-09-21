using System.Collections.Concurrent;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

/// <summary>
/// Name → spell-icon index per shard, built from the charplan catalogs.
/// Loaded once per shard on first use; hits for abilities the catalog has no
/// icon for simply render without one.
/// </summary>
internal sealed class AbilityIconIndex
{
    private readonly ICatalogOverrideRepository _overrides;
    private readonly ConcurrentDictionary<ShardType, IReadOnlyDictionary<string, IconSpriteRef>> _cache = new();

    public AbilityIconIndex(ICatalogOverrideRepository overrides)
    {
        _overrides = overrides;
    }

    public IReadOnlyDictionary<string, IconSpriteRef> Get(ShardType shard)
    {
        return _cache.GetOrAdd(shard, Build);
    }

    public void Invalidate()
    {
        _cache.Clear();
    }

    private IReadOnlyDictionary<string, IconSpriteRef> Build(ShardType shard)
    {
        try
        {
            var entries = shard switch
            {
                ShardType.Eden => EdenDataBrowserCatalog.Load(_overrides.LoadCatalogEntryOverrides()),
                ShardType.Blackthorn => BlackthornDataBrowserCatalog.Load(_overrides.LoadCatalogEntryOverrides()),
                _ => []
            };

            return entries
                .Where(x => x.Icon is not null)
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Icon!,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, IconSpriteRef>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
