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

    [Fact]
    public void ProfileResolver_FindsWindowInsideFeatureFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "ui", "custom");
        var featureDir = Path.Combine(packageDir, "stats-pack");
        var fontDir = Path.Combine(packageDir, "stats-pack", "fonts");
        Directory.CreateDirectory(featureDir);
        Directory.CreateDirectory(fontDir);
        try
        {
            // Root stub forwards into the feature folder; the real template and
            // the font catalog are feature-owned.
            File.WriteAllText(Path.Combine(packageDir, "uimain.xml"), """
                <Root_Element><Include>stats-pack/custom4_window.xml</Include></Root_Element>
                """);
            File.WriteAllText(Path.Combine(featureDir, "custom4_window.xml"), """
                <Root_Element><WindowTemplate><Name>custom4_window</Name>
                  <ScalarLabelDef><ControlId>dex_data</ControlId><Position><X>55</X><Y>0</Y></Position><FontName>feature_font</FontName><Width>40</Width><Height>15</Height><Adapter>stats_dexterity</Adapter></ScalarLabelDef>
                  <ScalarLabelDef><ControlId>qui_data</ControlId><Position><X>55</X><Y>15</Y></Position><FontName>feature_font</FontName><Width>40</Width><Height>15</Height><Adapter>stats_quickness</Adapter></ScalarLabelDef>
                </WindowTemplate></Root_Element>
                """);
            File.WriteAllText(Path.Combine(featureDir, "fonts", "fonts.xml"), """
                <Root_Element><Font><Name>feature_font</Name><File>custom/stats-pack/fonts/test.tga</File></Font></Root_Element>
                """);
            File.WriteAllBytes(Path.Combine(fontDir, "test.tga"), [0]);

            var watch = new OcrWatchRegion("Custom4", "Custom4", new ScreenRegion(0, 0, 100, 30));
            var profile = DaocUiBitmapFontProfileResolver.ResolveFromGameRoot(watch, root);

            Assert.NotNull(profile);
            Assert.Equal("feature_font", profile.FontName);
            Assert.Equal(Path.Combine(featureDir, "custom4_window.xml"), profile.WindowPath);
            Assert.Equal(Path.Combine(fontDir, "test.tga"), profile.FontPath);
            Assert.Equal(2, profile.Fields.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProfileResolver_CustomPathAcceptsGameUiAndPackageRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "ui", "custom");
        var featureDir = Path.Combine(packageDir, "feature");
        var fontDir = Path.Combine(featureDir, "fonts");
        Directory.CreateDirectory(fontDir);
        try
        {
            File.WriteAllText(Path.Combine(featureDir, "window.xml"), """
                <Root_Element><WindowTemplate><Name>custom5_window</Name>
                  <ScalarLabelDef><ControlId>a</ControlId><Position><X>5</X><Y>0</Y></Position><FontName>feature_font</FontName><Width>40</Width><Height>15</Height><Adapter>stats_strength</Adapter></ScalarLabelDef>
                  <ScalarLabelDef><ControlId>b</ControlId><Position><X>5</X><Y>15</Y></Position><FontName>feature_font</FontName><Width>40</Width><Height>15</Height><Adapter>stats_constitution</Adapter></ScalarLabelDef>
                </WindowTemplate></Root_Element>
                """);
            File.WriteAllText(Path.Combine(featureDir, "fonts", "fonts.xml"), """
                <Root_Element><Font><Name>feature_font</Name><File>custom/feature/fonts/test.tga</File></Font></Root_Element>
                """);
            File.WriteAllBytes(Path.Combine(fontDir, "test.tga"), [0]);

            var watch = new OcrWatchRegion("Custom5", "Custom5", new ScreenRegion(0, 0, 100, 30));

            Assert.NotNull(DaocUiBitmapFontProfileResolver.ResolveFromCustomPath(watch, root));
            Assert.NotNull(DaocUiBitmapFontProfileResolver.ResolveFromCustomPath(watch, Path.Combine(root, "ui")));
            Assert.NotNull(DaocUiBitmapFontProfileResolver.ResolveFromCustomPath(watch, packageDir));
            var mainManifest = Path.Combine(packageDir, "uimain.xml");
            File.WriteAllText(mainManifest, "<Root_Element />");
            Assert.NotNull(DaocUiBitmapFontProfileResolver.ResolveFromCustomPath(watch, mainManifest));
            Assert.Null(DaocUiBitmapFontProfileResolver.ResolveFromCustomPath(watch, Path.Combine(root, "missing")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProfileResolver_SkipsMalformedFilesDuringScan()
    {
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "ui", "custom");
        var catalogDir = Path.Combine(packageDir, "runtime", "catalogs");
        var fontDir = Path.Combine(packageDir, "fonts");
        var realDir = Path.Combine(packageDir, "real");
        Directory.CreateDirectory(catalogDir);
        Directory.CreateDirectory(fontDir);
        Directory.CreateDirectory(realDir);
        try
        {
            // A broken flat file mentions the window name but must not abort the scan.
            File.WriteAllText(Path.Combine(packageDir, "custom6_window.xml"),
                "<WindowTemplate><Name>custom6_window</Name>");
            File.WriteAllText(Path.Combine(realDir, "windows.xml"), """
                <Root_Element><WindowTemplate><Name>custom6_window</Name>
                  <ScalarLabelDef><ControlId>a</ControlId><Position><X>5</X><Y>0</Y></Position><FontName>f</FontName><Width>40</Width><Height>15</Height><Adapter>stats_strength</Adapter></ScalarLabelDef>
                  <ScalarLabelDef><ControlId>b</ControlId><Position><X>5</X><Y>15</Y></Position><FontName>f</FontName><Width>40</Width><Height>15</Height><Adapter>stats_constitution</Adapter></ScalarLabelDef>
                </WindowTemplate></Root_Element>
                """);
            File.WriteAllText(Path.Combine(catalogDir, "fonts.xml"), """
                <Root_Element><Font><Name>f</Name><File>custom/fonts/test.tga</File></Font></Root_Element>
                """);
            File.WriteAllBytes(Path.Combine(fontDir, "test.tga"), [0]);

            var watch = new OcrWatchRegion("Custom6", "Custom6", new ScreenRegion(0, 0, 100, 30));
            var profile = DaocUiBitmapFontProfileResolver.ResolveFromGameRoot(watch, root);

            Assert.NotNull(profile);
            Assert.EndsWith(Path.Combine("real", "windows.xml"), profile.WindowPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProfileResolver_ReturnsNullWhenWindowMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "ui", "custom"));
        try
        {
            var watch = new OcrWatchRegion("Custom9", "Custom9", new ScreenRegion(0, 0, 100, 30));
            Assert.Null(DaocUiBitmapFontProfileResolver.ResolveFromGameRoot(watch, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProfileResolver_ResolvesAdapterOnlyControlsWithDataLabels()
    {
        // Mirrors custom/currency/currency.xml: LabelDef controls bind an
        // Adapter and carry the rendered sample in Data ("M:99") with no
        // ControlId. The label is the Data prefix minus value characters.
        var root = Path.Combine(Path.GetTempPath(), $"heraldhelper-ui-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(root, "ui", "custom");
        var featureDir = Path.Combine(packageDir, "currency");
        var fontDir = Path.Combine(featureDir, "fonts");
        Directory.CreateDirectory(fontDir);
        try
        {
            File.WriteAllText(Path.Combine(featureDir, "currency.xml"), """
                <Root_Element><WindowTemplate><Name>custom13_window</Name>
                  <LabelDef><Position><X>0</X><Y>0</Y></Position><FontName>currency_font_title</FontName><Width>60</Width><Height>12</Height><Adapter>money_mithril</Adapter><Data>M:99</Data></LabelDef>
                  <LabelDef><Position><X>0</X><Y>12</Y></Position><FontName>currency_font_title</FontName><Width>60</Width><Height>12</Height><Adapter>money_platinum</Adapter><Data>P:199</Data></LabelDef>
                  <LabelDef><Position><X>0</X><Y>24</Y></Position><FontName>currency_font_title</FontName><Width>60</Width><Height>12</Height><Adapter>money_gold</Adapter><Data>G:123</Data></LabelDef>
                </WindowTemplate></Root_Element>
                """);
            File.WriteAllText(Path.Combine(featureDir, "fonts", "fonts.xml"), """
                <Root_Element><Font><Name>currency_font_title</Name><File>custom/currency/fonts/font-title.tga</File></Font></Root_Element>
                """);
            File.WriteAllBytes(Path.Combine(fontDir, "font-title.tga"), [0]);

            var watch = new OcrWatchRegion("Custom13", "Custom13", new ScreenRegion(0, 0, 100, 40));
            var profile = DaocUiBitmapFontProfileResolver.ResolveFromGameRoot(watch, root);

            Assert.NotNull(profile);
            Assert.Equal("currency_font_title", profile.FontName);
            Assert.Equal(Path.Combine(fontDir, "font-title.tga"), profile.FontPath);
            Assert.Equal(new[] { "M:", "P:", "G:" }, profile.Fields.Select(field => field.Label).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RuntimeSettings_ReadCustomUiFolder()
    {
        var settings = HeraldHelper.Infrastructure.Configuration.AppRuntimeSettings.FromMap(
            new Dictionary<string, string> { ["customUiFolder"] = "D:\\uis\\custom" });
        Assert.Equal("D:\\uis\\custom", settings.CustomUiFolder);

        var empty = HeraldHelper.Infrastructure.Configuration.AppRuntimeSettings.FromMap(
            new Dictionary<string, string>());
        Assert.Null(empty.CustomUiFolder);
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
