namespace HeraldHelper.Application.Models;

public sealed record CastEvent(
    CastEventType EventType,
    string? SpellName,
    int OccurrenceOrdinal = 1);
