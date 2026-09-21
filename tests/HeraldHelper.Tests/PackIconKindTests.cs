using System.Text.RegularExpressions;
using MaterialDesignThemes.Wpf;

namespace HeraldHelper.Tests;

/// <summary>PackIcon.Kind resolves at runtime — a typo'd kind compiles fine
/// and crashes the app on window load (startup-error.log). Validate every
/// Kind="..." in the repo's XAML against the real enum.</summary>
public sealed class PackIconKindTests
{
    private static readonly Regex KindRegex = new(@"Kind=""(?<kind>\w+)""", RegexOptions.Compiled);

    [Fact]
    public void AllPackIconKindsInXaml_AreValid()
    {
        var repoRoot = FindRepoRoot();
        var valid = new HashSet<string>(Enum.GetNames<PackIconKind>(), StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(repoRoot, "src"), "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match match in KindRegex.Matches(File.ReadAllText(file)))
            {
                var kind = match.Groups["kind"].Value;
                if (!valid.Contains(kind))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {kind}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeraldHelper.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate the repo root.");
    }
}
