using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Contracts;

public interface IOcrReplaySink
{
    void Record(
        ShardType shard,
        string characterName,
        DateTimeOffset capturedUtc,
        IReadOnlyList<OcrReplayCapture> captures,
        ChatParseResult parseResult);
}
