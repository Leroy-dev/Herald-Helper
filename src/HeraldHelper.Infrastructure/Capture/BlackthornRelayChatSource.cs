using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Experimental Blackthorn-only chat source: connects to the launcher's BTUI
/// realtime relay (ws://127.0.0.1:&lt;port&gt;/events) — the same push channel the
/// in-client CEF windows use. Chat arrives as structured events (Type 1001
/// ChatMessage, Type 1003 CommandCenterMessage with chatType/chatLocation
/// metadata) with no file buffering. Non-chat watch regions route to the
/// provided fallback (OCR or chat.log tailer).
/// </summary>
public sealed class BlackthornRelayChatSource : IChatCaptureService, IWindowAwareChatCaptureService, IDisposable, IChatCaptureSourceTelemetry
{
    private readonly IWindowAwareChatCaptureService _fallback;
    private readonly Func<BlackthornRelayDiscovery.Endpoint?> _discover;
    private readonly string _surface;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly HashSet<string> _regionKeys = new(StringComparer.OrdinalIgnoreCase) { "chat" };
    private readonly object _sync = new();
    private readonly Queue<string> _pendingLines = new();
    private readonly CancellationTokenSource _stop = new();
    private BlackthornRelayDiscovery.Endpoint? _endpoint;
    private DateTime _lastProbe = DateTime.MinValue;
    private Task? _receiveLoop;
    private bool _started;
    private int _eventsSeen;
    private int _chatLinesSeen;

    public BlackthornRelayChatSource(
        IWindowAwareChatCaptureService fallback,
        Func<BlackthornRelayDiscovery.Endpoint?> discover,
        string surface = "heraldhelper",
        IEnumerable<string>? regionKeys = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _fallback = fallback;
        _discover = discover;
        _surface = surface;
        if (regionKeys is not null)
        {
            foreach (var key in regionKeys.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                _regionKeys.Add(key.Trim());
            }
        }
        _diagnostics = diagnostics;
    }

    private bool _servedChat;

    public string LastChatSource =>
        _servedChat ? "bt-relay" : (_fallback as IChatCaptureSourceTelemetry)?.LastChatSource ?? "OCR";

    public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
    {
        _servedChat = true;
        return Task.FromResult(DrainLines());
    }

    public Task<string> CaptureWindowTextAsync(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken)
    {
        if (!_regionKeys.Contains(watchRegion.Key) && !_regionKeys.Contains(watchRegion.Label))
        {
            return _fallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
        }
        _servedChat = true;
        return Task.FromResult(DrainLines());
    }

    /// <summary>Post a command to the relay command channel (e.g. SendCommandCenterCommand "who").</summary>
    public async Task<bool> SendCommandAsync(string command, object? payload, CancellationToken cancellationToken)
    {
        var endpoint = _endpoint;
        if (endpoint is null)
        {
            return false;
        }
        try
        {
            var url = $"{endpoint.CommandsUrl}?token={endpoint.Token}&surface={_surface}";
            using var content = new StringContent(
                JsonSerializer.Serialize(new { command, payload }), Encoding.UTF8, "text/plain");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await http.PostAsync(url, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _diagnostics?.Log($"[BtRelay] command '{command}' failed: {ex.Message}");
            return false;
        }
    }

    private string DrainLines()
    {
        EnsureStarted();
        lock (_sync)
        {
            if (_pendingLines.Count == 0)
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            while (_pendingLines.Count > 0)
            {
                sb.Append(_pendingLines.Dequeue()).Append('\n');
            }
            return sb.ToString();
        }
    }

    private void EnsureStarted()
    {
        lock (_sync)
        {
            if (_endpoint is not null || DateTime.UtcNow - _lastProbe < TimeSpan.FromSeconds(5))
            {
                // Endpoint known, or discovery probed recently — just ensure the loop runs.
                if (_endpoint is not null && !_started)
                {
                    _started = true;
                    _receiveLoop = Task.Run(() => ReceiveLoopAsync(_stop.Token));
                }
                return;
            }

            _lastProbe = DateTime.UtcNow;
            _endpoint = _discover();
            if (_endpoint is not null)
            {
                _diagnostics?.Log($"[BtRelay] discovered {_endpoint.EventsUrl}");
                _started = true;
                _receiveLoop = Task.Run(() => ReceiveLoopAsync(_stop.Token));
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken stop)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!stop.IsCancellationRequested)
        {
            var endpoint = _endpoint;
            if (endpoint is null)
            {
                return;
            }
            try
            {
                using var ws = new ClientWebSocket();
                var uri = new Uri($"{endpoint.EventsUrl}?token={endpoint.Token}&surface={_surface}");
                await ws.ConnectAsync(uri, stop);
                backoff = TimeSpan.FromSeconds(1);
                _diagnostics?.Log($"[BtRelay] connected {uri.Host}:{uri.Port} surface={_surface}");
                await ReceiveAsync(ws, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or HttpRequestException)
            {
                _diagnostics?.Log($"[BtRelay] disconnected: {ex.Message}; retry in {backoff.TotalSeconds:0}s");
            }

            try
            {
                await Task.Delay(backoff, stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    private async Task ReceiveAsync(ClientWebSocket ws, CancellationToken stop)
    {
        var buffer = new byte[64 * 1024];
        var carry = new MemoryStream();
        while (ws.State == WebSocketState.Open && !stop.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(buffer, stop);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                continue;
            }
            carry.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }
            var raw = Encoding.UTF8.GetString(carry.GetBuffer(), 0, (int)carry.Length);
            carry.SetLength(0);
            HandleEvent(raw);
        }
    }

    private void HandleEvent(string raw)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("Type", out var typeEl) || !root.TryGetProperty("Data", out var data))
            {
                return;
            }

            var isChat = typeEl.ValueKind == JsonValueKind.Number
                ? typeEl.TryGetInt32(out var n) && n is 1001 or 1003
                : typeEl.ValueKind == JsonValueKind.String &&
                  (typeEl.GetString() is "ChatMessage" or "CommandCenterMessage");
            if (!isChat)
            {
                return;
            }

            var line = data.TryGetProperty("raw", out var rawEl) && rawEl.ValueKind == JsonValueKind.String
                ? rawEl.GetString()
                : data.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String
                    ? msgEl.GetString()
                    : null;
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (_sync)
            {
                _eventsSeen++;
                _chatLinesSeen++;
                _pendingLines.Enqueue(line);
            }
        }
    }

    public int EventsSeen { get { lock (_sync) return _eventsSeen; } }
    public int ChatLinesSeen { get { lock (_sync) return _chatLinesSeen; } }

    public void Dispose()
    {
        _stop.Cancel();
    }
}
