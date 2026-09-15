using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

internal sealed record DaocGlyphReadResult(
    string Text,
    string FontName,
    int RecognizedFields,
    int TotalFields,
    double Confidence);

internal sealed class DaocBitmapFontGlyphReader
{
    private static readonly Regex NumericValueRegex = new(
        @"^[+-]?\d{1,5}%?$",
        RegexOptions.CultureInvariant);

    private readonly Dictionary<string, CachedProfile> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _uiPathOverride;

    public DaocBitmapFontGlyphReader(string? uiPathOverride = null)
    {
        _uiPathOverride = string.IsNullOrWhiteSpace(uiPathOverride) ? null : uiPathOverride.Trim();
    }

    public bool TryRead(
        Bitmap source,
        OcrWatchRegion watchRegion,
        ShardType shardType,
        out DaocGlyphReadResult result)
    {
        result = new DaocGlyphReadResult(string.Empty, string.Empty, 0, 0, 0);
        try
        {
            var cached = GetProfile(watchRegion, shardType);
            if (cached is null)
            {
                return false;
            }

            var lines = new List<string>();
            var confidences = new List<double>();
            foreach (var field in cached.Profile.Fields)
            {
                if (!TryReadField(source, field, cached.Font, out var value, out var confidence))
                {
                    continue;
                }

                lines.Add($"{field.Label} {value}");
                confidences.Add(confidence);
            }

            var minimumFields = Math.Max(2, (cached.Profile.Fields.Count + 1) / 2);
            if (lines.Count < minimumFields)
            {
                return false;
            }

            var averageConfidence = confidences.Average();
            if (averageConfidence < 0.58)
            {
                return false;
            }

            result = new DaocGlyphReadResult(
                string.Join(Environment.NewLine, lines),
                cached.Profile.FontName,
                lines.Count,
                cached.Profile.Fields.Count,
                averageConfidence);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int ResolveCaptureWidth(OcrWatchRegion watchRegion, ShardType shardType)
    {
        try
        {
            var cached = GetProfile(watchRegion, shardType);
            if (cached is null || !cached.Profile.Fields.Any(field => IsResistanceAdapter(field.Adapter)))
            {
                return watchRegion.Region.Width;
            }
            return CalculateCaptureWidth(cached.Profile, watchRegion.Region.Width);
        }
        catch
        {
            return watchRegion.Region.Width;
        }
    }

    internal static int CalculateCaptureWidth(DaocUiBitmapFontProfile profile, int configuredWidth)
    {
        var resistanceFields = profile.Fields.Where(field => IsResistanceAdapter(field.Adapter)).ToArray();
        if (resistanceFields.Length == 0)
        {
            return configuredWidth;
        }

        // Resistance controls render signed percentages beyond the small
        // WindowTemplate width. Capture their visible overflow without
        // widening unrelated custom windows such as character stats.
        var requiredWidth = resistanceFields.Max(field => field.X + Math.Min(field.Width, 72));
        return Math.Max(configuredWidth, requiredWidth);
    }

    private CachedProfile? GetProfile(OcrWatchRegion watchRegion, ShardType shardType)
    {
        var key = $"{_uiPathOverride}|{shardType}:{watchRegion.Key}";
        if (_cache.TryGetValue(key, out var cached) && cached.IsCurrent())
        {
            return cached;
        }

        var profile = DaocUiBitmapFontProfileResolver.Resolve(watchRegion, shardType, _uiPathOverride);
        if (profile is null || !DaocBitmapFont.TryLoad(profile.FontPath, out var font))
        {
            _cache.Remove(key);
            return null;
        }

        cached = new CachedProfile(
            profile,
            font,
            File.GetLastWriteTimeUtc(profile.WindowPath),
            File.GetLastWriteTimeUtc(profile.FontPath));
        _cache[key] = cached;
        return cached;
    }

    internal static bool TryReadField(
        Bitmap source,
        DaocUiValueField field,
        DaocBitmapFont font,
        out string value,
        out double confidence)
    {
        value = string.Empty;
        confidence = 0;
        var left = Math.Clamp(field.X, 0, source.Width);
        var top = Math.Clamp(field.Y, 0, source.Height);
        var right = Math.Clamp(field.X + field.Width, left, source.Width);
        var bottom = Math.Clamp(field.Y + Math.Min(field.Height, font.RowHeight), top, source.Height);
        if (right <= left || bottom <= top)
        {
            return false;
        }

        var mask = CreateSourceMask(source, left, top, right - left, bottom - top);
        var runs = FindColumnRuns(mask);
        if (runs.Count == 0)
        {
            return false;
        }

        var recognized = RecognizeRuns(mask, runs, font);
        var candidate = recognized.Text;
        var scores = recognized.Scores;
        if (!NumericValueRegex.IsMatch(candidate) || scores.Count != candidate.Length)
        {
            return false;
        }

        var adapterIsResistance = IsResistanceAdapter(field.Adapter);
        if (adapterIsResistance && !candidate.EndsWith('%'))
        {
            // These compact DAoC windows often clip the last pixels of '%'.
            // The adapter supplies the unit unambiguously, so preserve the
            // fully recognized signed value and restore only that suffix.
            candidate += '%';
        }

        value = candidate;
        confidence = scores.Average();
        return true;
    }

    private static bool IsResistanceAdapter(string adapter) =>
        adapter.Contains("thrust", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("crush", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("slash", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("heat", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("cold", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("matter", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("energy", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("spirit", StringComparison.OrdinalIgnoreCase) ||
        adapter.Contains("body", StringComparison.OrdinalIgnoreCase);

    private static (string Text, List<double> Scores) RecognizeRuns(
        bool[,] mask,
        IReadOnlyList<ColumnRun> runs,
        DaocBitmapFont font,
        double minimumScore = 0.48)
    {
        var text = new StringBuilder();
        var scores = new List<double>();
        foreach (var run in runs.Take(7))
        {
            var pattern = Crop(mask, run.Start, 0, run.Width, mask.GetLength(1));
            pattern = CropToContent(pattern);
            if (pattern.GetLength(0) == 0 || pattern.GetLength(1) == 0)
            {
                continue;
            }

            var (bestCharacter, bestScore) = MatchPattern(pattern, font.NumericGlyphs);
            if (bestScore < minimumScore && run.Start + run.Width == mask.GetLength(0))
            {
                // The compact resistance panel can clip '%' and connect its
                // first pixels to the final digit. Match the complete prefix
                // glyph and ignore only the incomplete edge remainder.
                foreach (var width in font.NumericGlyphs.Select(x => x.Mask.GetLength(0)).Distinct())
                {
                    if (width >= run.Width)
                    {
                        continue;
                    }
                    var prefix = Crop(mask, run.Start, 0, width, mask.GetLength(1));
                    prefix = CropToContent(prefix);
                    var prefixMatch = MatchPattern(prefix, font.NumericGlyphs);
                    if (prefixMatch.Score > bestScore)
                    {
                        (bestCharacter, bestScore) = prefixMatch;
                    }
                }
            }

            if (bestCharacter != '\0' && bestScore >= minimumScore)
            {
                text.Append(bestCharacter);
                scores.Add(bestScore);
            }
        }

        return (text.ToString(), scores);
    }

    private static (char Character, double Score) MatchPattern(
        bool[,] pattern,
        IReadOnlyList<DaocGlyphPattern> glyphs)
    {
        var bestCharacter = '\0';
        var bestScore = 0d;
        foreach (var glyph in glyphs)
        {
            var score = Compare(pattern, glyph.Mask);
            if (score > bestScore)
            {
                bestScore = score;
                bestCharacter = glyph.Character;
            }
        }
        return (bestCharacter, bestScore);
    }

    private static bool[,] CreateSourceMask(Bitmap source, int left, int top, int width, int height)
    {
        var mask = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = source.GetPixel(left + x, top + y);
                var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                var neutralText = brightness >= 155 && minimum >= 105 && maximum - minimum <= 105;
                var yellowText = pixel.R >= 225 && pixel.G >= 200 && pixel.B <= 130;
                mask[x, y] = neutralText || yellowText;
            }
        }
        return mask;
    }

    private static List<ColumnRun> FindColumnRuns(bool[,] mask)
    {
        var width = mask.GetLength(0);
        var height = mask.GetLength(1);
        var runs = new List<ColumnRun>();
        var start = -1;
        for (var x = 0; x <= width; x++)
        {
            var occupied = false;
            if (x < width)
            {
                for (var y = 0; y < height; y++)
                {
                    if (mask[x, y])
                    {
                        occupied = true;
                        break;
                    }
                }
            }

            if (occupied && start < 0)
            {
                start = x;
            }
            else if (!occupied && start >= 0)
            {
                runs.Add(new ColumnRun(start, x - start));
                start = -1;
            }
        }
        return runs;
    }

    private static double Compare(bool[,] source, bool[,] template)
    {
        var sourceWidth = source.GetLength(0);
        var sourceHeight = source.GetLength(1);
        var templateWidth = template.GetLength(0);
        var templateHeight = template.GetLength(1);
        if (Math.Abs(sourceWidth - templateWidth) > 3 || Math.Abs(sourceHeight - templateHeight) > 3)
        {
            return 0;
        }

        var best = 0d;
        for (var offsetY = -2; offsetY <= 2; offsetY++)
        {
            for (var offsetX = -2; offsetX <= 2; offsetX++)
            {
                var sourceHits = 0;
                var templateHits = 0;
                var sourceMatched = 0;
                var templateMatched = 0;
                var exactMatches = 0;
                for (var y = 0; y < sourceHeight; y++)
                {
                    for (var x = 0; x < sourceWidth; x++)
                    {
                        if (!source[x, y])
                        {
                            continue;
                        }
                        sourceHits++;
                        if (HasPixel(template, x - offsetX, y - offsetY))
                        {
                            exactMatches++;
                        }
                        if (HasPixelNear(template, x - offsetX, y - offsetY))
                        {
                            sourceMatched++;
                        }
                    }
                }
                for (var y = 0; y < templateHeight; y++)
                {
                    for (var x = 0; x < templateWidth; x++)
                    {
                        if (!template[x, y])
                        {
                            continue;
                        }
                        templateHits++;
                        if (HasPixelNear(source, x + offsetX, y + offsetY))
                        {
                            templateMatched++;
                        }
                    }
                }

                if (sourceHits == 0 || templateHits == 0)
                {
                    continue;
                }

                var precision = (double)sourceMatched / sourceHits;
                var recall = (double)templateMatched / templateHits;
                var tolerantScore = (2 * precision * recall) / (precision + recall + double.Epsilon);
                var exactScore = (2d * exactMatches) / (sourceHits + templateHits);
                var score = (exactScore * 0.75) + (tolerantScore * 0.25);
                score -= (Math.Abs(sourceWidth - templateWidth) + Math.Abs(sourceHeight - templateHeight)) * 0.025;
                best = Math.Max(best, score);
            }
        }
        return Math.Clamp(best, 0, 1);
    }

    private static bool HasPixel(bool[,] mask, int x, int y) =>
        x >= 0 && x < mask.GetLength(0) &&
        y >= 0 && y < mask.GetLength(1) &&
        mask[x, y];

    private static bool HasPixelNear(bool[,] mask, int x, int y)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var candidateX = x + offsetX;
                var candidateY = y + offsetY;
                if (candidateX >= 0 && candidateX < mask.GetLength(0) &&
                    candidateY >= 0 && candidateY < mask.GetLength(1) &&
                    mask[candidateX, candidateY])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool[,] CropToContent(bool[,] source)
    {
        var minX = source.GetLength(0);
        var minY = source.GetLength(1);
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < source.GetLength(1); y++)
        {
            for (var x = 0; x < source.GetLength(0); x++)
            {
                if (!source[x, y])
                {
                    continue;
                }
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
        return maxX < minX ? new bool[0, 0] : Crop(source, minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static bool[,] Crop(bool[,] source, int left, int top, int width, int height)
    {
        var result = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                result[x, y] = source[left + x, top + y];
            }
        }
        return result;
    }

    private sealed record CachedProfile(
        DaocUiBitmapFontProfile Profile,
        DaocBitmapFont Font,
        DateTime WindowLastWriteUtc,
        DateTime FontLastWriteUtc)
    {
        public bool IsCurrent() =>
            File.Exists(Profile.WindowPath) &&
            File.Exists(Profile.FontPath) &&
            File.GetLastWriteTimeUtc(Profile.WindowPath) == WindowLastWriteUtc &&
            File.GetLastWriteTimeUtc(Profile.FontPath) == FontLastWriteUtc;
    }

    private readonly record struct ColumnRun(int Start, int Width);
}

internal sealed class DaocBitmapFont
{
    private static readonly char[] NumericCharacters = "0123456789+-%".ToCharArray();

    private DaocBitmapFont(int rowHeight, IReadOnlyList<DaocGlyphPattern> numericGlyphs)
    {
        RowHeight = rowHeight;
        NumericGlyphs = numericGlyphs;
    }

    public int RowHeight { get; }
    public IReadOnlyList<DaocGlyphPattern> NumericGlyphs { get; }

    public static bool TryLoad(string path, out DaocBitmapFont font)
    {
        font = null!;
        try
        {
            var image = DaocTgaImage.Load(path);
            var rectangles = ExtractMarkerLayout(image, out var rowHeight);
            if (rectangles.Count < 64 || rowHeight <= 0)
            {
                return false;
            }

            var glyphs = new List<DaocGlyphPattern>();
            foreach (var character in NumericCharacters)
            {
                var index = character - 33;
                if (index < 0 || index >= rectangles.Count)
                {
                    return false;
                }

                var rectangle = rectangles[index];
                var mask = CreateGlyphMask(image, rectangle);
                mask = CropGlyphToContent(mask);
                if (mask.GetLength(0) == 0 || mask.GetLength(1) == 0)
                {
                    return false;
                }
                glyphs.Add(new DaocGlyphPattern(character, mask));
            }

            font = new DaocBitmapFont(rowHeight, glyphs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<GlyphRectangle> ExtractMarkerLayout(DaocTgaImage image, out int rowHeight)
    {
        var glyphs = new List<GlyphRectangle>(256);
        var rowStart = 0;
        rowHeight = 0;
        for (var y = 0; y < image.Height; y++)
        {
            if (!IsGuidePixel(image, 0, y))
            {
                continue;
            }

            var currentHeight = y - rowStart;
            rowHeight = Math.Max(rowHeight, currentHeight);
            var inGlyph = false;
            var glyphStart = 0;
            for (var x = 1; x < image.Width; x++)
            {
                var marker = IsGuidePixel(image, x, y);
                if (marker && !inGlyph)
                {
                    inGlyph = true;
                    glyphStart = x;
                }
                else if (!marker && inGlyph)
                {
                    inGlyph = false;
                    if (x > glyphStart && currentHeight > 0)
                    {
                        glyphs.Add(new GlyphRectangle(glyphStart, rowStart, x - glyphStart, currentHeight));
                    }
                }
            }
            rowStart = y + 1;
        }
        return glyphs;
    }

    private static bool IsGuidePixel(DaocTgaImage image, int x, int y)
    {
        var pixel = image.GetPixel(x, y);
        return pixel.A == byte.MaxValue;
    }

    private static bool[,] CreateGlyphMask(DaocTgaImage image, GlyphRectangle rectangle)
    {
        var mask = new bool[rectangle.Width, rectangle.Height];
        var brightPixels = 0;
        for (var y = 0; y < rectangle.Height; y++)
        {
            for (var x = 0; x < rectangle.Width; x++)
            {
                var pixel = image.GetPixel(rectangle.X + x, rectangle.Y + y);
                var brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                mask[x, y] = pixel.A >= 32 && brightness >= 96;
                if (mask[x, y])
                {
                    brightPixels++;
                }
            }
        }

        if (brightPixels > 0)
        {
            return mask;
        }

        for (var y = 0; y < rectangle.Height; y++)
        {
            for (var x = 0; x < rectangle.Width; x++)
            {
                mask[x, y] = image.GetPixel(rectangle.X + x, rectangle.Y + y).A >= 32;
            }
        }
        return mask;
    }

    private static bool[,] CropGlyphToContent(bool[,] source)
    {
        var minX = source.GetLength(0);
        var minY = source.GetLength(1);
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < source.GetLength(1); y++)
        {
            for (var x = 0; x < source.GetLength(0); x++)
            {
                if (!source[x, y])
                {
                    continue;
                }
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX)
        {
            return new bool[0, 0];
        }

        var result = new bool[maxX - minX + 1, maxY - minY + 1];
        for (var y = 0; y < result.GetLength(1); y++)
        {
            for (var x = 0; x < result.GetLength(0); x++)
            {
                result[x, y] = source[minX + x, minY + y];
            }
        }
        return result;
    }

    private readonly record struct GlyphRectangle(int X, int Y, int Width, int Height);
}

internal sealed record DaocGlyphPattern(char Character, bool[,] Mask);
