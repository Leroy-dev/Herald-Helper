namespace HeraldHelper.Domain.Models;

public sealed record CastSpellOverride(
    string SourceName,
    string Name,
    double? CastTimeSeconds,
    IconSpriteRef? Icon,
    string? ClassName = null,
    int? Level = null);
