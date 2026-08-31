using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class DaocWindowParsingTests
{
    [Fact]
    public void CustomWindow_UsesUiXmlSizeInsteadOfIniStatusFields()
    {
        var sizes = new Dictionary<string, DaocUiWindowSize>(StringComparer.OrdinalIgnoreCase)
        {
            ["Custom3"] = new(63, 123, "custom3_window.xml")
        };

        var parsed = DaocCharacterDiscoveryService.TryParseWindowDefinition(
            "Custom3=456,1161,1,100,1",
            sizes,
            out var window);

        Assert.True(parsed);
        Assert.Equal(456, window.Region.X);
        Assert.Equal(1161, window.Region.Y);
        Assert.Equal(63, window.Region.Width);
        Assert.Equal(123, window.Region.Height);
        Assert.False(window.SizeIsEstimated);
    }

    [Fact]
    public void CustomWindow_UsesMarkedFallbackWhenUiXmlIsUnavailable()
    {
        var parsed = DaocCharacterDiscoveryService.TryParseWindowDefinition(
            "Custom4=520,1169,1,100,1",
            new Dictionary<string, DaocUiWindowSize>(),
            out var window);

        Assert.True(parsed);
        Assert.Equal(68, window.Region.Width);
        Assert.Equal(112, window.Region.Height);
        Assert.True(window.SizeIsEstimated);
        Assert.Contains("~68x112", window.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatWindow_KeepsDocumentedIniRectangle()
    {
        var parsed = DaocCharacterDiscoveryService.TryParseWindowDefinition(
            "ChatWindow0=Main,1,1103,456,335,80,80,13,1,0,1",
            new Dictionary<string, DaocUiWindowSize>(),
            out var window);

        Assert.True(parsed);
        Assert.Equal("Main", window.Label);
        Assert.Equal(1, window.Region.X);
        Assert.Equal(1103, window.Region.Y);
        Assert.Equal(456, window.Region.Width);
        Assert.Equal(335, window.Region.Height);
    }

    [Fact]
    public void UiResolver_ReadsWindowTemplateDimensions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "custom3_window.xml"), """
                <Root_Element ID="DAOCUi">
                  <WindowTemplate>
                    <Name>custom3_window</Name>
                    <Width>63</Width>
                    <Height>123</Height>
                    <FullResizeImageDef><Width>999</Width><Height>999</Height></FullResizeImageDef>
                  </WindowTemplate>
                </Root_Element>
                """);

            var result = DaocUiWindowSizeResolver.LoadFromUiRoots([directory]);

            Assert.Equal(63, result["Custom3"].Width);
            Assert.Equal(123, result["Custom3"].Height);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
