namespace HeraldHelper.Application.Models;

public sealed class ReplayFrameDiff
{
    public int FrameIndex { get; }
    public IReadOnlyList<string> Messages { get; }

    public bool IsEmpty => Messages.Count == 0;

    public ReplayFrameDiff(int frameIndex, IReadOnlyList<string> messages)
    {
        FrameIndex = frameIndex;
        Messages = messages;
    }
}
