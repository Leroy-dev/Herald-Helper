using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface IHeraldClient
{
    Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken);
}
