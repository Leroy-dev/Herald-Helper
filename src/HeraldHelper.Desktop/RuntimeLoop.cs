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
    private CancellationTokenSource _tickCts = new();
    private Task? _activeTick;

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
        if (_tickCts.IsCancellationRequested)
        {
            _tickCts = new CancellationTokenSource();
        }
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        // Cooperative cancel — a wedged capture (heap scan, dead OCR child)
        // can't hold the loop hostage past a Stop.
        _tickCts.Cancel();
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
        var ct = _tickCts.Token;
        var tick = Task.Run(() => _session.TickAsync(
            input.ChatRegion,
            input.Shard,
            input.ResistPercent,
            ct));
        _activeTick = tick;
        try
        {
            // Capture (RPM/OCR) is blocking work — run it off the UI thread so
            // the window stays responsive at 350ms cadence. Subscribers must
            // marshal to the dispatcher for UI updates.
            var result = await tick;
            _lastFailure = null;
            _sameFailureCount = 0;
            TickCompleted?.Invoke(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stop/Rebuild cancelled the in-flight tick — not a failure.
        }
        catch (Exception ex)
        {
            ReportFailure($"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _tickInProgress = false;
            _activeTick = null;
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
        _tickCts.Cancel();
        // Give an in-flight tick a moment to unwind before its session is
        // torn down — capture sources honor the token on their await points,
        // but a long heap scan may not; either way the session is disposed.
        var tick = _activeTick;
        if (tick is not null)
        {
            try
            {
                tick.Wait(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Faulted or still running — dispose the session regardless.
            }
        }
        _session.Dispose();
        _tickCts.Dispose();
    }
}

internal sealed record LoopTickInput(ScreenRegion? ChatRegion, ShardType Shard, int ResistPercent);

internal sealed record LoopTickResult(
    string Output,
    OverlaySnapshot? Snapshot,
    string DiagnosticsText,
    IReadOnlyDictionary<string, string>? AdapterValues = null);
