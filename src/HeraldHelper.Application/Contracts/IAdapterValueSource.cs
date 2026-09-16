namespace HeraldHelper.Application.Contracts;

/// <summary>
/// Live adapter values observed on a capture channel — e.g. the chat.log tail
/// picks up `/showadapter name` output lines ("name (scalar): 70.000",
/// "name (text): \"...\""). Keys are adapter names, values are the raw printed
/// value (quotes stripped for text adapters).
/// </summary>
public interface IAdapterValueSource
{
    IReadOnlyDictionary<string, string> LatestAdapterValues { get; }
}
