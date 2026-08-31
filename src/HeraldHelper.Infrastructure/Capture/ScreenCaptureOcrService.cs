using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Ocr;

namespace HeraldHelper.Infrastructure.Capture;

public sealed class ScreenCaptureOcrService : IChatCaptureService, IWindowAwareChatCaptureService, IOcrCaptureSnapshotSource, IOcrCaptureBatchDiagnostics
{
    private readonly IOcrEngine _ocrEngine;
    private readonly DaocBitmapFontGlyphReader _glyphReader = new();
    private readonly List<CaptureMetric> _batchMetrics = [];
    private bool _batchActive;
    private string _lastCaptureEngineName = string.Empty;
    public string LastOcrEngineName { get; private set; } = string.Empty;
    public long LastOcrDurationMs { get; private set; }
    public int LastOcrTextLength { get; private set; }
    public byte[]? LastCapturePng { get; private set; }
    public string LastCaptureEngineName => _lastCaptureEngineName;

    public ScreenCaptureOcrService(IOcrEngine ocrEngine)
    {
        _ocrEngine = ocrEngine;
    }

    public void BeginCaptureBatch()
    {
        _batchMetrics.Clear();
        _batchActive = true;
    }

    public void CompleteCaptureBatch()
    {
        if (!_batchActive)
        {
            return;
        }

        _batchActive = false;
        if (_batchMetrics.Count == 0)
        {
            LastOcrEngineName = string.Empty;
            LastOcrDurationMs = 0;
            LastOcrTextLength = 0;
            return;
        }

        LastOcrEngineName = string.Join(" | ", _batchMetrics.Select(metric => $"{metric.Label}: {metric.Engine}"));
        LastOcrDurationMs = _batchMetrics.Sum(metric => metric.DurationMs);
        LastOcrTextLength = _batchMetrics.Sum(metric => metric.TextLength);
    }

    public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken) =>
        CaptureTextAsync(region, null, ShardType.Default, cancellationToken);

    public Task<string> CaptureWindowTextAsync(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken) =>
        CaptureTextAsync(watchRegion.Region, watchRegion, shardType, cancellationToken);

    private async Task<string> CaptureTextAsync(
        ScreenRegion region,
        OcrWatchRegion? watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException("Capture region must have positive width and height.", nameof(region));
        }

        var captureWidth = watchRegion is null
            ? region.Width
            : _glyphReader.ResolveCaptureWidth(watchRegion, shardType);
        var isSmallUiText = region.Width <= 100 && region.Height >= 70;
        var tempPaths = new List<string>();

        try
        {
            using var bitmap = new Bitmap(captureWidth, region.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(region.X, region.Y, 0, 0, new Size(captureWidth, region.Height));
            }

            var rawCapturePng = EncodePng(bitmap);

            var sw = Stopwatch.StartNew();
            if (watchRegion is not null &&
                _glyphReader.TryRead(bitmap, watchRegion, shardType, out var glyphResult))
            {
                sw.Stop();

                var engine = $"DaocBitmap/{glyphResult.FontName} " +
                             $"({glyphResult.RecognizedFields}/{glyphResult.TotalFields}, " +
                             $"{glyphResult.Confidence:P0})";
                SetCaptureMetrics(watchRegion, engine, sw.ElapsedMilliseconds, glyphResult.Text);
                LastCapturePng = rawCapturePng;
                return glyphResult.Text;
            }

            var result = isSmallUiText
                ? await ReadSmallUiTextAsync(bitmap, tempPaths, cancellationToken)
                : await ReadStandardTextAsync(bitmap, tempPaths, cancellationToken);
            sw.Stop();

            var fallbackEngine = isSmallUiText
                ? $"{_ocrEngine.Name} + SmallUi5x/{result.Variant}"
                : _ocrEngine.Name;
            SetCaptureMetrics(watchRegion, fallbackEngine, sw.ElapsedMilliseconds, result.Text);
            LastCapturePng = rawCapturePng;
            return result.Text;
        }
        finally
        {
            foreach (var tempPath in tempPaths)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    internal void SetCaptureMetrics(OcrWatchRegion? watchRegion, string engine, long durationMs, string text)
    {
        var textLength = text.Count(static character => !char.IsWhiteSpace(character));
        _lastCaptureEngineName = engine;
        if (_batchActive)
        {
            _batchMetrics.Add(new CaptureMetric(ResolveDiagnosticLabel(watchRegion), engine, durationMs, textLength));
            return;
        }

        LastOcrEngineName = engine;
        LastOcrDurationMs = durationMs;
        LastOcrTextLength = textLength;
    }

    private static string ResolveDiagnosticLabel(OcrWatchRegion? watchRegion)
    {
        if (watchRegion is null ||
            watchRegion.Key.Equals("chat", StringComparison.OrdinalIgnoreCase) ||
            watchRegion.Key.StartsWith("ChatWindow", StringComparison.OrdinalIgnoreCase))
        {
            return "Chat";
        }
        return string.IsNullOrWhiteSpace(watchRegion.Label) ? watchRegion.Key : watchRegion.Label;
    }

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private async Task<(string Text, string Path, string Variant)> ReadStandardTextAsync(
        Bitmap source,
        ICollection<string> tempPaths,
        CancellationToken cancellationToken)
    {
        var path = CreateTempPath("chat");
        tempPaths.Add(path);
        source.Save(path, ImageFormat.Png);
        return (await _ocrEngine.ReadTextAsync(path, cancellationToken), path, "standard");
    }

    private async Task<(string Text, string Path, string Variant)> ReadSmallUiTextAsync(
        Bitmap source,
        ICollection<string> tempPaths,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(string Text, string Path, string Variant)>();
        foreach (var variant in new[] { "solid2", "solid", "outline" })
        {
            var path = CreateTempPath($"small-ui-{variant}");
            tempPaths.Add(path);
            using var image = variant switch
            {
                "solid2" => PrepareSolidSmallUiText(source, dilationRadius: 2),
                "solid" => PrepareSolidSmallUiText(source, dilationRadius: 1),
                _ => PrepareOutlineSmallUiText(source)
            };
            image.Save(path, ImageFormat.Png);
            candidates.Add((await _ocrEngine.ReadTextAsync(path, cancellationToken), path, variant));
        }

        var best = candidates
            .OrderByDescending(candidate => ScoreSmallUiText(candidate.Text))
            .ThenBy(candidate => candidate.Variant == "solid2" ? 0 : candidate.Variant == "solid" ? 1 : 2)
            .First();

        return best;
    }

    private static string CreateTempPath(string prefix) => Path.Combine(
        Path.GetTempPath(),
        $"heraldhelper-{prefix}-{Guid.NewGuid():N}.png");

    private static int ScoreSmallUiText(string text)
    {
        var normalized = text.ToLowerInvariant();
        // A readable numeric value is more useful for the stats parser than a
        // label alone, so favor variants that retain digits after cleanup.
        var score = normalized.Count(char.IsDigit) * 4;
        foreach (var marker in SmallUiMarkers)
        {
            if (normalized.Contains(marker, StringComparison.Ordinal))
            {
                score += 20;
            }
        }

        return score;
    }

    private static readonly string[] SmallUiMarkers =
    [
        "str", "con", "coh", "dex", "dez", "qui", "int", "emp", "cha",
        "thr", "cru", "sla", "hea", "col", "mat", "ene", "spi", "bod"
    ];

    private sealed record CaptureMetric(string Label, string Engine, long DurationMs, int TextLength);

    // DAoC custom panels use a tiny bitmap font. A crisp monochrome enlargement
    // gives the Windows OCR engine enough glyph detail without changing chat OCR.
    private static Bitmap PrepareOutlineSmallUiText(Bitmap source)
    {
        using var thresholded = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                var isText = brightness >= 118 || (pixel.R >= 130 && pixel.G >= 105 && pixel.B <= 115);
                thresholded.SetPixel(x, y, isText ? Color.White : Color.Black);
            }
        }

        const int scale = 5;
        var enlarged = new Bitmap(source.Width * scale, source.Height * scale, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(enlarged);
        graphics.Clear(Color.Black);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.DrawImage(
            thresholded,
            new Rectangle(0, 0, enlarged.Width, enlarged.Height),
            0,
            0,
            thresholded.Width,
            thresholded.Height,
            GraphicsUnit.Pixel);
        return enlarged;
    }

    // The game font has a dark shadow inside each yellow glyph. This variant
    // turns it into solid dark text on white, which Windows OCR reads better.
    private static Bitmap PrepareSolidSmallUiText(Bitmap source, int dilationRadius)
    {
        using var textMask = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                var isText = brightness >= 118 || (pixel.R >= 130 && pixel.G >= 105 && pixel.B <= 115);
                textMask.SetPixel(x, y, isText ? Color.Black : Color.White);
            }
        }

        using var thickened = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var hasTextNeighbor = false;
                for (var offsetY = -dilationRadius; offsetY <= dilationRadius && !hasTextNeighbor; offsetY++)
                {
                    for (var offsetX = -dilationRadius; offsetX <= dilationRadius; offsetX++)
                    {
                        var neighborX = x + offsetX;
                        var neighborY = y + offsetY;
                        if (neighborX >= 0 && neighborX < source.Width && neighborY >= 0 && neighborY < source.Height &&
                            textMask.GetPixel(neighborX, neighborY).R == 0)
                        {
                            hasTextNeighbor = true;
                            break;
                        }
                    }
                }

                thickened.SetPixel(x, y, hasTextNeighbor ? Color.Black : Color.White);
            }
        }

        const int scale = 5;
        var enlarged = new Bitmap(source.Width * scale, source.Height * scale, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(enlarged);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.DrawImage(thickened, new Rectangle(0, 0, enlarged.Width, enlarged.Height));
        return enlarged;
    }
}
