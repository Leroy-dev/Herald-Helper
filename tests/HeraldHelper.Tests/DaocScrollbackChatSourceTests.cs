using System.Text;
using HeraldHelper.Infrastructure.Capture;
using Xunit;

namespace HeraldHelper.Tests;

public class DaocScrollbackChatSourceTests
{
    [Fact]
    public void ExtractStrings_PullsPrintableRuns()
    {
        var buf = Encoding.ASCII.GetBytes("\0\0You say, \"hi\"\0\x01\x02 casts a spell!\0ab\0");
        var strings = DaocScrollbackChatSource.ExtractStrings(buf).ToArray();
        Assert.Contains("You say, \"hi\"", strings);
        Assert.Contains(" casts a spell!", strings);
        Assert.DoesNotContain("ab", strings); // below the 4-char floor
    }

    [Fact]
    public void TrimToLineStart_StripsMarkerPrefixes()
    {
        Assert.Equal("Devilsorc was just killed", DaocScrollbackChatSource.TrimToLineStart("X9>Devilsorc was just killed"));
        Assert.Equal("Grailknight was just killed", DaocScrollbackChatSource.TrimToLineStart("r=Grailknight was just killed"));
        Assert.Equal("[Guild] name: text", DaocScrollbackChatSource.TrimToLineStart("0B[Guild] name: text"));
    }

    [Fact]
    public void TrimToLineStart_KeepsCleanLines()
    {
        Assert.Equal("You say, \"hi\"", DaocScrollbackChatSource.TrimToLineStart("You say, \"hi\""));
        Assert.Equal("[Guild] name: text", DaocScrollbackChatSource.TrimToLineStart("[Guild] name: text"));
    }

    [Theory]
    [InlineData("You say, \"hello there\"", true)]
    [InlineData("[Guild] Name: some words here", true)]
    [InlineData("Bonejamin was just killed by Mingming in The", true)]
    [InlineData("Bip01 L Finger01", false)]
    [InlineData("Editable Mesh", false)]
    [InlineData("Scene Root", false)]
    [InlineData("G 7B", false)]
    [InlineData("xq", false)]
    [InlineData("C:\\path\\to\\file", false)]
    public void LooksLikeChat_FiltersAssetsAndNoise(string input, bool expected) =>
        Assert.Equal(expected, DaocScrollbackChatSource.LooksLikeChat(input));

    [Theory]
    [InlineData("Ashhat was just killed by Vaggoss in Hvedrungr Hill Ruins (Jamtland Mountains).", true)]
    [InlineData("[Guild] name: some text!", true)]
    [InlineData("was just killed by Mingming in The", false)] // no sentence end
    [InlineData("Bip01 R Finger02", false)]
    public void IsCompleteLine_RequiresSentenceEnd(string input, bool expected) =>
        Assert.Equal(expected, DaocScrollbackChatSource.IsCompleteLine(input));
}
