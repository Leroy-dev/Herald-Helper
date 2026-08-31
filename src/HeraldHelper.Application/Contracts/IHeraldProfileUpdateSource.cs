using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface IHeraldProfileUpdateSource
{
    event Action<TargetProfile>? TargetProfileUpdated;
}
