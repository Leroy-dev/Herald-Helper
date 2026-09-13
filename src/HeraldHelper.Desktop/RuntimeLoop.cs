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
        if (input.ChatRegion is null && _session.RuntimeSettings.OcrWatchRegions.Count == 0)
        {
            TickFailed?.Invoke("Select chat area first (drag selection).");
            return;
        }

        _tickInProgress = true;
        try
        {
            var result = await _session.TickAsync(
                input.ChatRegion,
                input.Shard,
                input.ResistPercent,
                CancellationToken.None);
            TickCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            TickFailed?.Invoke($"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _tickInProgress = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _session.Dispose();
    }
}

internal sealed record LoopTickInput(ScreenRegion? ChatRegion, ShardType Shard, int ResistPercent);

internal sealed record LoopTickResult(string Output, OverlaySnapshot? Snapshot, string DiagnosticsText);
