namespace HeraldHelper.Domain.Models;

public sealed record OverlaySnapshot(
    TargetProfile? Target,
    IReadOnlyCollection<CcTimerEntry> Timers,
    CastBarState? ActiveCast,
    string RawOcrText);
