namespace HeraldHelper.Infrastructure.Casting;

internal static class CastNameMatcher
{
    public static string? FindClosest(IEnumerable<string> availableNames, string requestedName)
    {
        var requested = Normalize(requestedName);
        if (requested.Length < 6)
        {
            return null;
        }
        var requestedWords = requested.Count(x => x == ' ') + 1;
        var maximum = requested.Length >= 14 ? 2 : 1;
        return availableNames
            .Select(name => new { Name = name, Distance = Distance(requested, name, maximum) })
            .Where(x => x.Name.Count(c => c == ' ') + 1 == requestedWords && x.Distance <= maximum)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Name.Length)
            .Select(x => x.Name)
            .FirstOrDefault();
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static int Distance(string left, string right, int maximum)
    {
        if (Math.Abs(left.Length - right.Length) > maximum) return maximum + 1;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var rowMinimum = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }
            if (rowMinimum > maximum) return maximum + 1;
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
