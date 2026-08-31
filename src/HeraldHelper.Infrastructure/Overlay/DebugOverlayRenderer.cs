using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Overlay;

public sealed class DebugOverlayRenderer : IOverlayRenderer
{
    public string LastRendered { get; private set; } = string.Empty;
    public OverlaySnapshot? LastSnapshot { get; private set; }

    public Task RenderAsync(OverlaySnapshot snapshot, CancellationToken cancellationToken)
    {
        var target = snapshot.Target;
        var timers = snapshot.Timers.Count;
        var cast = snapshot.ActiveCast;
        var targetLabel = target?.Name ?? "None";
        var details = target is null
            ? string.Empty
            : $" | Class: {target.Class ?? "-"} | Level: {(target.Level?.ToString() ?? "-")} | RR: {target.RealmRank ?? "-"} | Guild: {target.Guild ?? "-"}";
        var castLabel = cast is null ? string.Empty : $" | Cast: {cast.SpellName} ({cast.TotalSeconds:0.0}s)";
        LastRendered = $"Target: {targetLabel}, Timers: {timers}{details}{castLabel}";
        LastSnapshot = snapshot;
        return Task.CompletedTask;
    }
}
