using System.Text;
using HeraldHelper.Application.Contracts;
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
    public IWindowAwareChatCaptureService CaptureChain { get; }

    public OverlaySnapshot? LastSnapshot => DebugOverlay.LastSnapshot;

    public RuntimeSession(
        GameLoopOrchestrator orchestrator,
        DebugOverlayRenderer debugOverlay,
        AppRuntimeSettings runtimeSettings,
        ScreenCaptureOcrService capture,
        IWindowAwareChatCaptureService captureChain)
    {
        Orchestrator = orchestrator;
        DebugOverlay = debugOverlay;
        RuntimeSettings = runtimeSettings;
        Capture = capture;
        CaptureChain = captureChain;
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
        var adapterValues = (CaptureChain as IAdapterValueSource)?.LatestAdapterValues;
        return new LoopTickResult(output, snapshot, BuildDiagnosticsText(snapshot, adapterValues), adapterValues);
    }

    private string BuildDiagnosticsText(OverlaySnapshot? snapshot, IReadOnlyDictionary<string, string>? adapterValues)
    {
        var raw = snapshot?.RawOcrText ?? string.Empty;
        if (raw.Length > 700)
        {
            raw = raw[..700] + " ...";
        }

        var sb = new StringBuilder();
        var chatSource = (CaptureChain as IChatCaptureSourceTelemetry)?.LastChatSource
            ?? (RuntimeSettings.ChatLogCaptureEnabled ? "chat.log" : "OCR");
        sb.AppendLine($"Chat source: {chatSource}");
        if (RuntimeSettings.ConservativeMode)
        {
            sb.AppendLine("Mode: conservative — process memory untouched");
        }
        // When scrollback is outermost but not serving, show why — "not
        // elevated" vs "no chat text yet" read identically as 'OCR' otherwise.
        if (CaptureChain is DaocScrollbackChatSource scrollback &&
            scrollback.LastChatSource != "scrollback" &&
            scrollback.BindError is { } scrollbackError)
        {
            sb.AppendLine($"Scrollback: {scrollbackError}");
        }
        if (adapterValues is { Count: > 0 })
        {
            sb.AppendLine($"Adapters: {adapterValues.Count} (memory)");
        }
        else if (CaptureChain is DaocMemoryStatsSource { MapBound: false } mem)
        {
            sb.AppendLine($"Adapters: not bound — {mem.BindError ?? "probing"}");
        }
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
