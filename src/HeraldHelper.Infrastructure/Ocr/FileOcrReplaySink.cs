using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Ocr;

public sealed class FileOcrReplaySink : IOcrReplaySink
{
    private const int MaxRecords = 250;
    private readonly string _root;
    private string? _lastContentHash;

    public FileOcrReplaySink(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeraldHelper",
            "ocr-replay");
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
        if (string.Equals(hash, _lastContentHash, StringComparison.Ordinal))
        {
            return;
        }
        _lastContentHash = hash;

        Directory.CreateDirectory(_root);
        var recordName = $"{capturedUtc:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        var recordDirectory = Path.Combine(_root, recordName);
        Directory.CreateDirectory(recordDirectory);
        for (var index = 0; index < captures.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(recordDirectory, $"capture-{index + 1}.png"), captures[index].PngBytes);
        }
        var metadata = new
        {
            schemaVersion = 1,
            capturedUtc,
            shard = shard.ToString(),
            characterName,
            captures = captures.Select((x, index) => new
            {
                image = $"capture-{index + 1}.png",
                x.Label,
                x.Region,
                x.EngineName,
                x.OcrText
            }),
            parseResult
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
}
