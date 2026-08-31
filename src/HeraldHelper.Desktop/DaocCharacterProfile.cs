using System.IO;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

public sealed record DaocCharacterProfile(
    ShardType Shard,
    string CharacterName,
    string ShardDirectory,
    string PrimaryIniFile,
    IReadOnlyList<string> IniFiles,
    IReadOnlyList<DaocWindowDefinition> Windows,
    int ChatWindowCount,
    int QuickbarCount,
    string? CommandWindowRaw)
{
    public string DisplayLabel => IniFiles.Count == 0
        ? CharacterName
        : $"{CharacterName} ({string.Join(", ", IniFiles.Select(GetIniSuffix))})";

    public string SummaryText =>
        $"INI: {string.Join(", ", IniFiles)} | Windows: {Windows.Count} | Chat windows: {ChatWindowCount} | Quickbars: {QuickbarCount} | Command window: {(string.IsNullOrWhiteSpace(CommandWindowRaw) ? "n/a" : "yes")}";

    private static string GetIniSuffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dashIndex = stem.LastIndexOf('-');
        if (dashIndex >= 0 && dashIndex < stem.Length - 1)
        {
            var suffix = stem[(dashIndex + 1)..];
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                return suffix;
            }
        }

        return stem;
    }
}
