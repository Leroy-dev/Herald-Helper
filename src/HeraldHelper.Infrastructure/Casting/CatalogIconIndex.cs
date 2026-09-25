using System.Text.Json;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Shared loader for the data/generated/icon-configs.json index —
/// maps a client icon id to its charplan sprite. Used by the shard cast-spell
/// catalogs and the server spell table.</summary>
internal static class CatalogIconIndex
{
    internal sealed record IndexedIcon(IconSpriteRef Icon, string MappingType);

    public static Dictionary<int, IndexedIcon> Load(string path)
    {
        var icons = new Dictionary<int, IndexedIcon>();
        if (!File.Exists(path))
        {
            return icons;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("requestedIconId", out var idElement) || !idElement.TryGetInt32(out var iconId))
            {
                continue;
            }

            if (!item.TryGetProperty("spriteSheet", out var sheetElement) || !item.TryGetProperty("sprite", out var spriteElement))
            {
                continue;
            }

            var spriteSheet = sheetElement.GetString();
            if (string.IsNullOrWhiteSpace(spriteSheet))
            {
                continue;
            }

            var candidate = new IconSpriteRef(
                spriteSheet,
                ReadInt(spriteElement, "x"),
                ReadInt(spriteElement, "y"),
                ReadInt(spriteElement, "width"),
                ReadInt(spriteElement, "height"),
                ReadNestedInt(item, "overlays", "border"),
                ReadNestedInt(item, "overlays", "spellBadge"),
                ReadCornerIndex(item, "upLeft"),
                ReadCornerIndex(item, "up"),
                ReadCornerIndex(item, "upRight"),
                ReadCornerIndex(item, "right"),
                ReadCornerIndex(item, "downRight"),
                ReadCornerIndex(item, "down"),
                ReadCornerIndex(item, "left"));
            var mappingType = item.TryGetProperty("mappingType", out var mappingTypeElement)
                ? mappingTypeElement.GetString() ?? string.Empty
                : string.Empty;

            if (!icons.TryGetValue(iconId, out var existing)
                || MappingPriority(mappingType) > MappingPriority(existing.MappingType))
            {
                icons[iconId] = new IndexedIcon(candidate, mappingType);
            }
        }

        return icons;
    }

    internal static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    internal static int ReadNestedInt(JsonElement element, string objectName, string propertyName)
    {
        return element.TryGetProperty(objectName, out var nested) &&
               nested.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    internal static int ReadCornerIndex(JsonElement element, string propertyName)
    {
        return element.TryGetProperty("overlays", out var overlays) &&
               overlays.TryGetProperty("corners", out var corners) &&
               corners.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static int MappingPriority(string mappingType)
    {
        return mappingType.ToLowerInvariant() switch
        {
            "spell" => 3,
            "direct" => 2,
            "style" => 1,
            _ => 0
        };
    }
}
