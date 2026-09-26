using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Contracts;

/// <summary>Maps client buff-icon ids to crowd-control effect kinds. Used to
/// badge group members whose icon strip (group_%dicon%d adapters) shows a CC
/// effect — more reliable than chat broadcasts: always visible regardless of
/// distance and stays accurate for the effect's whole duration.</summary>
public interface ICcIconIndex
{
    ControlEffectType? Resolve(int iconId);
}
