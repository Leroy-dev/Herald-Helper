using System.Text;
using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Configuration;
using HeraldHelper.Infrastructure.Overlay;

namespace HeraldHelper.Desktop;

internal sealed class RuntimeSession : IRuntimeSession
{
    public GameLoopOrchestrator Orchestrator { get; }
    public DebugOverlayRenderer DebugOverlay { get; }
    public AppRuntimeSettings RuntimeSettings { get; }
    public ScreenCaptureOcrService Capture { get; }

    public OverlaySnapshot? LastSnapshot => DebugOverlay.LastSnapshot;

    public RuntimeSession(
        GameLoopOrchestrator orchestrator,
        DebugOverlayRenderer debugOverlay,
        AppRuntimeSettings runtimeSettings,
        ScreenCaptureOcrService capture)
    {
        Orchestrator = orchestrator;
        DebugOverlay = debugOverlay;
        RuntimeSettings = runtimeSettings;
        Capture = capture;
    }

    public async Task<LoopTickResult> TickAsync(
        ScreenRegion? chatRegion,
        ShardType shard,
        int resistPercent,
        CancellationToken cancellationToken)
    {
        await Orchestrator.TickAsync(
            chatRegion ?? new ScreenRegion(0, 0, 1, 1),
            shard,
            resistPercent,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var output = DebugOverlay.LastRendered;
        var snapshot = DebugOverlay.LastSnapshot;
        return new LoopTickResult(output, snapshot, BuildDiagnosticsText(snapshot));
    }

    private string BuildDiagnosticsText(OverlaySnapshot? snapshot)
    {
        var raw = snapshot?.RawOcrText ?? string.Empty;
        if (raw.Length > 700)
        {
            raw = raw[..700] + " ...";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Engine: {Capture.LastOcrEngineName}");
        sb.AppendLine($"OCR time: {Capture.LastOcrDurationMs} ms");
        sb.AppendLine($"OCR chars: {Capture.LastOcrTextLength}");
        sb.AppendLine($"Target class: {snapshot?.Target?.Class ?? "Unknown"}");
        sb.AppendLine("OCR preview:");
        sb.AppendLine(raw);
        return sb.ToString();
    }

    public void Dispose()
    {
        Orchestrator.Dispose();
    }
}
