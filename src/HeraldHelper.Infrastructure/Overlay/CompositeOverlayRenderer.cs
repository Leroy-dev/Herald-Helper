using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Overlay;

public sealed class CompositeOverlayRenderer : IOverlayRenderer
{
    private readonly IReadOnlyList<IOverlayRenderer> _renderers;

    public CompositeOverlayRenderer(IEnumerable<IOverlayRenderer> renderers)
    {
        _renderers = renderers.ToList();
    }

    public async Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var renderer in _renderers)
        {
            await renderer.RenderAsync(snapshot, cancellationToken);
        }
    }
}
