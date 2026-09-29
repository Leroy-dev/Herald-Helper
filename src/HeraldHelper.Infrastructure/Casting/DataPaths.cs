namespace HeraldHelper.Infrastructure.Casting;

/// <summary>Locates the bundled <c>data/</c> tree — dev builds run from
/// bin/… while <c>data/</c> sits at the repo root, packaged builds ship it
/// beside the exe. Every loader walks up from the output dir; this is the
/// single implementation (previously ~6 copies).</summary>
public static class DataPaths
{
    /// <summary>First ancestor containing <c>data/&lt;segments&gt;</c> as a
    /// directory, or null.</summary>
    public static string? FindDirectory(params string[] segments)
    {
        foreach (var dir in WalkUp())
        {
            var candidate = Path.Combine([dir.FullName, "data", .. segments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>First ancestor containing <c>data/&lt;segments&gt;</c> as a
    /// file, or null.</summary>
    public static string? FindFile(params string[] segments)
    {
        foreach (var dir in WalkUp())
        {
            var candidate = Path.Combine([dir.FullName, "data", .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>First ancestor that itself contains a <c>data</c> dir —
    /// returns the <c>data</c> path (matches FindDataRoot semantics).</summary>
    public static string? FindDataRoot()
    {
        foreach (var dir in WalkUp())
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>First ancestor that itself contains a <c>data</c> dir —
    /// returns the ANCESTOR (not the data path).</summary>
    public static string? FindProjectRoot()
    {
        foreach (var dir in WalkUp())
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    private static IEnumerable<DirectoryInfo> WalkUp()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }
}
