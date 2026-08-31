using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface IOverlayRenderer
{
    Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken);
}
