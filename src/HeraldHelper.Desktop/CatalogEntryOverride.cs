using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

public sealed record CatalogEntryOverride(
    string EntryKey,
    string Name,
    string EntryType,
    string ClassName,
    string Category,
    int? Level,
    double? CastTimeSeconds,
    string Summary,
    string Details,
    IconSpriteRef? Icon);
