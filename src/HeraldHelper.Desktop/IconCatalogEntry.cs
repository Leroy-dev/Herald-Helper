using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal sealed record IconCatalogEntry(
    int IconId,
    string SpriteSheet,
    int SpriteClass,
    string MappingType,
    IconSpriteRef Icon);
