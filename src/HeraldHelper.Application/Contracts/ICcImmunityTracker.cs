using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface ICcImmunityTracker
{
    void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc);
    IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc);
}
