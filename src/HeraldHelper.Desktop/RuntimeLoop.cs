using System.Windows.Threading;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Desktop;

internal sealed class RuntimeLoop : IDisposable
{
    private readonly IRuntimeSession _session;
    private readonly Func<LoopTickInput> _input;
    private readonly DispatcherTimer _timer;
    private bool _tickInProgress;
    private string? _lastFailure;
    private int _sameFailureCount;

    public RuntimeLoop(IRuntimeSession session, Func<LoopTickInput> input, TimeSpan interval)
    {
        _session = session;
        _input = input;
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += async (_, _) => await TickOnceAsync();
    }

    public bool IsRunning => _timer.IsEnabled;

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public AppRuntimeSettings RuntimeSettings => _session.RuntimeSettings;

    public OverlaySnapshot? LastSnapshot => _session.LastSnapshot;

    public event Action<LoopTickResult>? TickCompleted;

    public event Action<string>? TickFailed;

    /// <summary>Raised when the loop stops itself (not via <see cref="Stop"/>).</summary>
    public event Action? Stopped;

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public async Task TickOnceAsync()
    {
        if (_tickInProgress)
        {
            return;
        }

        var input = _input();
        // A non-OCR chat source (memory read, chat.log tail, BT relay) covers
        // chat without a dragged region — don't block the loop for it.
        var nonOcrChat = _session.RuntimeSettings.EffectiveChatMemReadEnabled
            || _session.RuntimeSettings.ChatLogCaptureEnabled
            || _session.RuntimeSettings.BlackthornRelayEnabled;
        if (input.ChatRegion is null && _session.RuntimeSettings.OcrWatchRegions.Count == 0 && !nonOcrChat)
        {
            ReportFailure("Select chat area first (drag selection).");
            return;
        }

        _tickInProgress = true;
        try
        {
            // Capture (RPM/OCR) is blocking work — run it off the UI thread so
            // the window stays responsive at 350ms cadence. Subscribers must
            // marshal to the dispatcher for UI updates.
            var result = await Task.Run(() => _session.TickAsync(
                input.ChatRegion,
                input.Shard,
                input.ResistPercent,
                CancellationToken.None));
            _lastFailure = null;
            _sameFailureCount = 0;
            TickCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            ReportFailure($"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _tickInProgress = false;
        }
    }

    private void ReportFailure(string message)
    {
        _sameFailureCount = string.Equals(message, _lastFailure, StringComparison.Ordinal)
            ? _sameFailureCount + 1
            : 0;
        _lastFailure = message;
        if (_sameFailureCount >= 20)
        {
            Stop();
            TickFailed?.Invoke($"Loop stopped — repeated failures: {message}");
            Stopped?.Invoke();
            return;
        }
        TickFailed?.Invoke(message);
    }

    public void Dispose()
    {
        _timer.Stop();
        _session.Dispose();
    }
}

internal sealed record LoopTickInput(ScreenRegion? ChatRegion, ShardType Shard, int ResistPercent);

internal sealed record LoopTickResult(
    string Output,
    OverlaySnapshot? Snapshot,
    string DiagnosticsText,
    IReadOnlyDictionary<string, string>? AdapterValues = null);
