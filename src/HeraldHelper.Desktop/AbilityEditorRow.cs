namespace HeraldHelper.Desktop;

public sealed class AbilityEditorRow
{
    public bool IsEnabled { get; set; } = true;
    public string AbilityName { get; set; } = string.Empty;
    public string SkillCode { get; set; } = "s";
    public int DurationSeconds { get; set; } = 1;
    public string EffectType { get; set; } = "s";
    public string Category { get; set; } = string.Empty;
    public int? Level { get; set; }
    public string Server { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string SourceAbilityName { get; set; } = string.Empty;
    public string SourceEffectType { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}
