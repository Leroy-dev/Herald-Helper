using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class GlobalHotkeyTests
{
    [Theory]
    [InlineData("Ctrl+F9", 0x0002, 120)]   // MOD_CONTROL, VK_F9 (NOREPEAT added at register time)
    [InlineData("F9", 0x0000, 120)]
    [InlineData("Ctrl+Shift+T", 0x0006, 0x54)]
    [InlineData("Alt+F1", 0x0001, 112)]
    public void TryParse_ValidGestures(string text, int expectedMods, int expectedVk)
    {
        Assert.True(GlobalHotkey.TryParse(text, out var mods, out var vk));
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Bogus+X99")]
    [InlineData("F99")]
    public void TryParse_RejectsInvalid(string text)
    {
        Assert.False(GlobalHotkey.TryParse(text, out _, out _));
    }
}
