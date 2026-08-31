using System.Collections.Concurrent;
using HeraldHelper.Application.Contracts;

namespace HeraldHelper.Desktop;

public sealed class ResponseDiagnosticsBuffer : IResponseDiagnostics
{
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly int _maxLines;

    public event Action<string>? LineAdded;

    public ResponseDiagnosticsBuffer(int maxLines = 500)
    {
        _maxLines = Math.Max(50, maxLines);
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _lines.Enqueue(line);
        Trim();
        LineAdded?.Invoke(line);
    }

    public IReadOnlyCollection<string> Snapshot()
    {
        return _lines.ToArray();
    }

    public void Clear()
    {
        while (_lines.TryDequeue(out _))
        {
        }
    }

    private void Trim()
    {
        while (_lines.Count > _maxLines && _lines.TryDequeue(out _))
        {
        }
    }
}
