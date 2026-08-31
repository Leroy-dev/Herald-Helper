namespace HeraldHelper.Application.Models;

public sealed class ReplayExpected
{
    public string? TargetName { get; set; }
    public string? TargetClass { get; set; }
    public bool? TargetIsLoading { get; set; }
    public string? ActiveCastSpellName { get; set; }
    public double? ActiveCastRemainingSeconds { get; set; }
    public int? AbilityHitCount { get; set; }
}
