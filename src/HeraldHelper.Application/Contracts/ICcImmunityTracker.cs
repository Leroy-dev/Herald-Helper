using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface ICcImmunityTracker
{
    void RegisterSuccessfulHit(AbilityHit hit, string? targetClass, int resistPercent, DateTimeOffset nowUtc);
    /// <summary>Removes timers created within <paramref name="maxAge"/> for
    /// the given targets — resist/immune messages can arrive a tick after
    /// the cast line that wrongly started the timer.</summary>
    void RetractFreshEntries(IEnumerable<string> targetNames, DateTimeOffset nowUtc, TimeSpan maxAge);
    IReadOnlyCollection<CcTimerEntry> GetActiveTimers(DateTimeOffset nowUtc);
}
