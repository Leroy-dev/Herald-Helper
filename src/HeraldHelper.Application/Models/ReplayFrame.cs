using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Models;

public sealed class ReplayFrame
{
    public int Index { get; set; }
    public string ChatOcrText { get; set; } = string.Empty;
    public ChatParseResult ParseResult { get; set; } = new(null, []);
    public ReplayExpected Expected { get; set; } = new();
    public ShardType Shard { get; set; } = ShardType.Eden;
    public int ResistPercent { get; set; } = 0;
    public TimeSpan? Advance { get; set; }
}
