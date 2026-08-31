namespace HeraldHelper.Domain.Models;

public sealed record CastBarState(
    string SpellName,
    double TotalSeconds,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    IconSpriteRef? Icon,
    double? EstimatedDamage = null,
    string? DamageType = null,
    double? BaseSeconds = null)
{
    public double Progress(DateTimeOffset nowUtc)
    {
        if (TotalSeconds <= 0)
        {
            return 1;
        }

        var elapsedSeconds = (nowUtc - StartedAtUtc).TotalSeconds;
        return Math.Clamp(elapsedSeconds / TotalSeconds, 0, 1);
    }

    public double RemainingSeconds(DateTimeOffset nowUtc)
    {
        return Math.Max(0, (EndsAtUtc - nowUtc).TotalSeconds);
    }

    public bool IsActive(DateTimeOffset nowUtc)
    {
        return nowUtc < EndsAtUtc;
    }
}
