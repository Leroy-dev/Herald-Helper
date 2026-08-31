namespace HeraldHelper.Desktop;

public sealed record AbilityProfileOverride(
    string Server,
    string ClassName,
    string CharacterName,
    string SourceAbilityName,
    string SourceEffectType,
    bool? IsEnabled,
    string? AbilityName,
    string? SkillCode,
    int? DurationSeconds,
    string? EffectType,
    string? Category,
    int? Level,
    bool HasLevelOverride,
    string? Aliases,
    bool IsCustom);
