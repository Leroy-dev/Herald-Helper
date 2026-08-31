using System.Drawing;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Ocr;

namespace HeraldHelper.Tests;

public sealed class DaocBitmapFontInfrastructureTests
{
    [Fact]
    public void TgaLoader_ReadsBottomOriginPixels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"heraldhelper-tga-{Guid.NewGuid():N}.tga");
        try
        {
            // Stored bottom row first: blue, white; then top row: red, green.
            WriteTga(path, 2, 2,
            [
                255, 0, 0, 255, 255, 255, 255, 255,
                0, 0, 255, 255, 0, 255, 0, 255
            ], topOrigin: false);

            var image = DaocTgaImage.Load(path);

            Assert.Equal((byte)255, image.GetPixel(0, 0).R);
            Assert.Equal((byte)255, image.GetPixel(1, 0).G);
            Assert.Equal((byte)255, image.GetPixel(0, 1).B);
            Assert.Equal((byte)255, image.GetPixel(1, 1).R);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TgaLoader_ReadsRleAndRejectsOversizedImages()
    {
        var rlePath = Path.Combine(Path.GetTempPath(), $"heraldhelper-rle-{Guid.NewGuid():N}.tga");
        var oversizedPath = Path.Combine(Path.GetTempPath(), $"heraldhelper-large-{Guid.NewGuid():N}.tga");
        try
        {
            var rle = CreateTgaHeader(width: 3, height: 1, imageType: 10, topOrigin: true);
            rle.AddRange([0x81, 0, 0, 255, 255]); // Two red pixels.
            rle.AddRange([0x00, 0, 255, 0, 255]); // One raw green pixel.
            File.WriteAllBytes(rlePath, [.. rle]);

            var image = DaocTgaImage.Load(rlePath);

            Assert.Equal((byte)255, image.GetPixel(0, 0).R);
            Assert.Equal((byte)255, image.GetPixel(1, 0).R);
            Assert.Equal((byte)255, image.GetPixel(2, 0).G);

            File.WriteAllBytes(oversizedPath, [.. CreateTgaHeader(5000, 1, 2, topOrigin: true)]);
            Assert.Throws<InvalidDataException>(() => DaocTgaImage.Load(oversizedPath));
        }
        finally
        {
            File.Delete(rlePath);
            File.Delete(oversizedPath);
        }
    }

    [Fact]
    public void BitmapFont_LoadsDaocMarkerAtlas()
    {
        const int glyphCount = 64;
        const int width = (glyphCount * 2) + 1;
        const int height = 4;
        var pixels = new byte[width * height * 4];
        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            var x = 1 + (glyph * 2);
            for (var y = 0; y < 3; y++)
            {
                SetWhitePixel(pixels, width, x, y);
            }
            SetWhitePixel(pixels, width, x, 3);
        }
        SetWhitePixel(pixels, width, 0, 3);

        var path = Path.Combine(Path.GetTempPath(), $"heraldhelper-font-{Guid.NewGuid():N}.tga");
        try
        {
            WriteTga(path, width, height, pixels, topOrigin: true);

            var loaded = DaocBitmapFont.TryLoad(path, out var font);

            Assert.True(loaded);
            Assert.Equal(3, font.RowHeight);
            Assert.Equal(13, font.NumericGlyphs.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("stats_dexterity", "188", "188")]
    [InlineData("stats_heat", "+29%", "+29%")]
    [InlineData("stats_heat", "+29", "+29%")]
    public void GlyphReader_ReadsValuesRenderedFromMarkerAtlas(string adapter, string rendered, string expected)
    {
        const int glyphCount = 64;
        const int glyphWidth = 5;
        const int glyphHeight = 7;
        const int cellWidth = glyphWidth + 1;
        var atlasWidth = (glyphCount * cellWidth) + 1;
        var atlasPixels = new byte[atlasWidth * (glyphHeight + 1) * 4];
        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            var left = 1 + (glyph * cellWidth);
            for (var x = 0; x < glyphWidth; x++)
            {
                SetWhitePixel(atlasPixels, atlasWidth, left + x, 0);
                SetWhitePixel(atlasPixels, atlasWidth, left + x, glyphHeight - 1);
                SetWhitePixel(atlasPixels, atlasWidth, left + x, glyphHeight);
            }
            for (var y = 1; y < glyphHeight - 1; y++)
            {
                SetWhitePixel(atlasPixels, atlasWidth, left, y);
                SetWhitePixel(atlasPixels, atlasWidth, left + glyphWidth - 1, y);
                for (var x = 1; x < glyphWidth - 1; x++)
                {
                    if (((glyph + (y * 7) + (x * 11)) & (1 << ((x + y) % 6))) != 0)
                    {
                        SetWhitePixel(atlasPixels, atlasWidth, left + x, y);
                    }
                }
            }
        }
        SetWhitePixel(atlasPixels, atlasWidth, 0, glyphHeight);

        var path = Path.Combine(Path.GetTempPath(), $"heraldhelper-match-{Guid.NewGuid():N}.tga");
        try
        {
            WriteTga(path, atlasWidth, glyphHeight + 1, atlasPixels, topOrigin: true);
            Assert.True(DaocBitmapFont.TryLoad(path, out var font));

            using var source = new Bitmap(60, 12);
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.Clear(Color.Black);
            }
            DrawValue(source, font, 2, 2, rendered, resistanceColors: adapter == "stats_heat");
            var field = new DaocUiValueField("Value:", adapter, 2, 2, 55, 8);

            var success = DaocBitmapFontGlyphReader.TryReadField(source, field, font, out var value, out var confidence);

            Assert.True(success);
            Assert.Equal(expected, value);
            Assert.True(confidence > 0.9);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CaptureWidth_OnlyIncludesResistanceOverflow()
    {
        var resistanceProfile = new DaocUiBitmapFontProfile(
            "window.xml",
            "font",
            "font.tga",
            [new DaocUiValueField("Thr:", "stats_thrust", 55, 0, 100, 15)]);
        var statsProfile = new DaocUiBitmapFontProfile(
            "window.xml",
            "font",
            "font.tga",
            [new DaocUiValueField("Dex:", "stats_dexterity", 55, 0, 100, 15)]);

        Assert.Equal(127, DaocBitmapFontGlyphReader.CalculateCaptureWidth(resistanceProfile, 95));
        Assert.Equal(100, DaocBitmapFontGlyphReader.CalculateCaptureWidth(statsProfile, 100));
    }

    [Fact]
    public void CaptureDiagnostics_AggregatesBatchButPreservesLastReplayEngine()
    {
        var capture = new ScreenCaptureOcrService(new UnusedOcrEngine());
        var chat = new OcrWatchRegion("ChatWindow0", ".", new ScreenRegion(0, 0, 10, 10));
        var stats = new OcrWatchRegion("Custom3", "Custom3", new ScreenRegion(0, 0, 10, 10));

        capture.BeginCaptureBatch();
        capture.SetCaptureMetrics(chat, "WindowsBuiltIn", 10, "abc d");
        capture.SetCaptureMetrics(stats, "DaocBitmap/test (10/10, 100%)", 5, "12");
        capture.CompleteCaptureBatch();

        Assert.Equal(
            "Chat: WindowsBuiltIn | Custom3: DaocBitmap/test (10/10, 100%)",
            capture.LastOcrEngineName);
        Assert.Equal(15, capture.LastOcrDurationMs);
        Assert.Equal(6, capture.LastOcrTextLength);
        Assert.Equal("DaocBitmap/test (10/10, 100%)", capture.LastCaptureEngineName);
    }

    [Fact]
    public void ProfileResolver_ToleratesDuplicateControlsAndUsesRowSpacing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        var windowDirectory = Path.Combine(root, "ui", "customs");
        var catalogDirectory = Path.Combine(root, "ui", "custom", "runtime", "catalogs");
        var fontDirectory = Path.Combine(root, "ui", "custom", "test", "fonts");
        Directory.CreateDirectory(windowDirectory);
        Directory.CreateDirectory(catalogDirectory);
        Directory.CreateDirectory(fontDirectory);
        try
        {
            File.WriteAllText(Path.Combine(windowDirectory, "custom3_window.xml"), """
                <Root_Element><WindowTemplate><Name>custom3_window</Name>
                  <LabelDef><ControlId>dex</ControlId><Data>Dex:</Data></LabelDef>
                  <LabelDef><ControlId>dex</ControlId><Data>Duplicate:</Data></LabelDef>
                  <ScalarLabelDef><ControlId>dex_data</ControlId><Position><X>55</X><Y>0</Y></Position><FontName>test_font</FontName><Width>40</Width><Height>25</Height><Adapter>stats_dexterity</Adapter></ScalarLabelDef>
                  <LabelDef><ControlId>qui</ControlId><Data>Qui:</Data></LabelDef>
                  <ScalarLabelDef><ControlId>qui_data</ControlId><Position><X>55</X><Y>15</Y></Position><FontName>test_font</FontName><Width>40</Width><Height>25</Height><Adapter>stats_quickness</Adapter></ScalarLabelDef>
                </WindowTemplate></Root_Element>
                """);
            File.WriteAllText(Path.Combine(catalogDirectory, "fonts.xml"), """
                <Root_Element><Font><Name>test_font</Name><File>custom/test/fonts/test.tga</File></Font></Root_Element>
                """);
            File.WriteAllBytes(Path.Combine(fontDirectory, "test.tga"), [0]);

            var watch = new OcrWatchRegion("Custom3", "Custom3", new ScreenRegion(0, 0, 100, 30));
            var profile = DaocUiBitmapFontProfileResolver.ResolveFromGameRoot(watch, root);

            Assert.NotNull(profile);
            Assert.Equal("test_font", profile.FontName);
            Assert.Equal(2, profile.Fields.Count);
            Assert.All(profile.Fields, field => Assert.Equal(15, field.Height));
            Assert.Equal("Dex:", profile.Fields[0].Label);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteTga(string path, int width, int height, byte[] pixels, bool topOrigin)
    {
        var bytes = CreateTgaHeader(width, height, imageType: 2, topOrigin);
        bytes.AddRange(pixels);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static List<byte> CreateTgaHeader(int width, int height, int imageType, bool topOrigin)
    {
        var bytes = new byte[18];
        bytes[2] = (byte)imageType;
        bytes[12] = (byte)(width & 0xff);
        bytes[13] = (byte)(width >> 8);
        bytes[14] = (byte)(height & 0xff);
        bytes[15] = (byte)(height >> 8);
        bytes[16] = 32;
        bytes[17] = (byte)(8 | (topOrigin ? 0x20 : 0));
        return [.. bytes];
    }

    private static void SetWhitePixel(byte[] pixels, int width, int x, int y)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset] = 255;
        pixels[offset + 1] = 255;
        pixels[offset + 2] = 255;
        pixels[offset + 3] = 255;
    }

    private static void DrawValue(
        Bitmap target,
        DaocBitmapFont font,
        int left,
        int top,
        string value,
        bool resistanceColors)
    {
        var cursor = left;
        foreach (var character in value)
        {
            var glyph = Assert.Single(font.NumericGlyphs, candidate => candidate.Character == character);
            for (var y = 0; y < glyph.Mask.GetLength(1); y++)
            {
                for (var x = 0; x < glyph.Mask.GetLength(0); x++)
                {
                    if (glyph.Mask[x, y])
                    {
                        var color = resistanceColors
                            ? character == '+' ? Color.FromArgb(255, 255, 63) : Color.FromArgb(208, 255, 181)
                            : Color.White;
                        target.SetPixel(cursor + x, top + y, color);
                    }
                }
            }
            cursor += glyph.Mask.GetLength(0) + 1;
        }
    }

    private sealed class UnusedOcrEngine : IOcrEngine
    {
        public string Name => "Unused";

        public Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
