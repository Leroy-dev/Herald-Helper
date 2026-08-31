using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class NullHeraldClient : IHeraldClient
{
    public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        return Task.FromResult<TargetProfile?>(null);
    }
}
