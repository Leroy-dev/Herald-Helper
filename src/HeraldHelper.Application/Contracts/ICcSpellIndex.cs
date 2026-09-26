using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Contracts;

/// <summary>Maps spell names to their crowd-control effect and real server
/// duration (server-spells.csv). Lets a completed cast of a known CC spell
/// turn the following broadcast apply line into a timer with the true
/// duration instead of the flat broadcast guess.</summary>
public interface ICcSpellIndex
{
    CcSpellInfo? Resolve(string spellName);
}

public sealed record CcSpellInfo(ControlEffectType Effect, int DurationSeconds);
