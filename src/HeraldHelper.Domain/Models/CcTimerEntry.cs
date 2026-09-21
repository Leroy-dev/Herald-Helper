using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Domain.Models;

public sealed record CcTimerEntry(
    string TargetName,
    ControlEffectType EffectType,
    DateTimeOffset ExpiresAtUtc,
    IconSpriteRef? Icon = null,
    string? TargetClass = null)
{
    public int RemainingSeconds(DateTimeOffset nowUtc)
    {
        var remaining = (int)Math.Ceiling((ExpiresAtUtc - nowUtc).TotalSeconds);
        return Math.Max(0, remaining);
    }
}
