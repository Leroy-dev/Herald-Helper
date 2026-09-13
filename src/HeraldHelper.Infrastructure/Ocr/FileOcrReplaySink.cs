using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Ocr;

/// <summary>
/// Records replay frames on a background drain task — Record() only hashes and
/// enqueues so the game loop never blocks on disk. Bounded queue drops the
/// oldest frames if the disk can't keep up.
/// </summary>
public sealed class FileOcrReplaySink : IOcrReplaySink, IDisposable
{
    private const int MaxRecords = 250;
    private const int MaxQueuedRecords = 30;

    private readonly string _root;
    private readonly Channel<QueuedRecord> _channel = Channel.CreateBounded<QueuedRecord>(
        new BoundedChannelOptions(MaxQueuedRecords)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    private readonly Task _drainTask;
    private readonly object _hashGate = new();
    private string? _lastContentHash;

    public FileOcrReplaySink(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeraldHelper",
            "ocr-replay");
        _drainTask = Task.Run(DrainAsync);
    }

    public void Record(
        ShardType shard,
        string characterName,
        DateTimeOffset capturedUtc,
        IReadOnlyList<OcrReplayCapture> captures,
        ChatParseResult parseResult)
    {
        if (captures.Count == 0)
        {
            return;
        }

        var combinedText = string.Join("\u001f", captures.Select(x => $"{x.Label}\u001e{x.OcrText}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combinedText)));
        lock (_hashGate)
        {
            if (string.Equals(hash, _lastContentHash, StringComparison.Ordinal))
            {
                return;
            }

            _lastContentHash = hash;
        }

        _channel.Writer.TryWrite(new QueuedRecord(shard, characterName, capturedUtc, captures, parseResult));
    }

    private async Task DrainAsync()
    {
        await foreach (var record in _channel.Reader.ReadAllAsync())
        {
            try
            {
                WriteRecord(record);
            }
            catch
            {
                // Replay writing must never break the game loop.
            }
        }
    }

    private void WriteRecord(QueuedRecord record)
    {
        Directory.CreateDirectory(_root);
        var recordName = $"{record.CapturedUtc:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        var recordDirectory = Path.Combine(_root, recordName);
        Directory.CreateDirectory(recordDirectory);
        for (var index = 0; index < record.Captures.Count; index++)
        {
            File.WriteAllBytes(
                Path.Combine(recordDirectory, $"capture-{index + 1}.png"),
                record.Captures[index].PngBytes);
        }

        var metadata = new
        {
            schemaVersion = 1,
            capturedUtc = record.CapturedUtc,
            shard = record.Shard.ToString(),
            characterName = record.CharacterName,
            captures = record.Captures.Select((x, index) => new
            {
                image = $"capture-{index + 1}.png",
                x.Label,
                x.Region,
                x.EngineName,
                x.OcrText
            }),
            parseResult = record.ParseResult
        };
        File.WriteAllText(
            Path.Combine(recordDirectory, "record.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
        TrimOldRecords();
    }

    private void TrimOldRecords()
    {
        foreach (var directory in new DirectoryInfo(_root)
                     .EnumerateDirectories()
                     .OrderByDescending(x => x.Name, StringComparer.Ordinal)
                     .Skip(MaxRecords))
        {
            try
            {
                directory.Delete(true);
            }
            catch
            {
                // A locked replay should not interrupt the game loop.
            }
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        try
        {
            _drainTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Best-effort flush on shutdown.
        }
    }

    private sealed record QueuedRecord(
        ShardType Shard,
        string CharacterName,
        DateTimeOffset CapturedUtc,
        IReadOnlyList<OcrReplayCapture> Captures,
        ChatParseResult ParseResult);
}
