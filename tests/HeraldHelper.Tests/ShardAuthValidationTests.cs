using HeraldHelper.Infrastructure.Auth;
using Xunit;

namespace HeraldHelper.Tests;

public class ShardAuthValidationTests
{
    [Fact]
    public void LoginButtonText_DeniesAnonymousPage()
    {
        // Eden hub renders a "LOGIN" nav button for anonymous sessions —
        // cookie presence alone must not count as authenticated.
        var hubText = "QUICK LINKS  HOME FORUM HUB  LOGIN  Home > Forum > Hub";
        Assert.True(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase(hubText, ["LOGIN"]));
    }

    [Fact]
    public void LoggedInPage_PassesDenyCheck()
    {
        // After login the nav shows the account, not the LOGIN button.
        var hubText = "QUICK LINKS  HOME FORUM HUB  ACCOUNT  Home > Forum > Hub";
        Assert.False(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase(hubText, ["LOGIN"]));
    }

    [Fact]
    public void EmptyOrMissingPhrases_NeverDeny()
    {
        Assert.False(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase("LOGIN page", null));
        Assert.False(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase("LOGIN page", []));
        Assert.False(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase(null, ["LOGIN"]));
    }

    [Fact]
    public void CaseInsensitive_Match()
    {
        Assert.True(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase("Please Login to continue", ["LOGIN"]));
        Assert.False(PlaywrightShardAuthRefreshService.ContainsAnyDenyPhrase("logged in as player", ["LOGIN"]));
    }
}
