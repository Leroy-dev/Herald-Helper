using System.Net;
using HeraldHelper.Infrastructure.Herald;

namespace HeraldHelper.Tests;

public sealed class BlackthornHeraldClientTests
{
    [Fact]
    public async Task GetTargetProfileAsync_ParsesCurrentPlayerPage()
    {
        const string html = """
            <table id="playerTable">
              <tr><th class="Name centerT">Name</th></tr>
              <tr class="large Hibernia"><td class="Name Lcell player">Teagan Presley</td></tr>
            </table>
            <table id="playerTable3">
              <tr><th class="guildName centerT">Guild</th></tr>
              <tr><td><a href="/stats/guild/Tir na Nog Adventurers">Tir na Nog Adventurers</a></td></tr>
            </table>
            <table class="w-75">
              <tr><th class="className centerT">Class</th></tr>
              <tr><td class="className Lcell icon"><img src="/assets/class/Nightshade_class_icon.webp"></td>
                  <td class="className centerT hover-yellow"><a href="/stats/players/realmpoints/all/Nightshade">Nightshade</a></td></tr>
            </table>
            <table class="playerLvl">
              <tr><th class="Level centerT">LVL</th><th class="RR centerT">RR</th></tr>
              <tr><td class="Level Lcell level centerT">50</td><td class="RR Lcell level centerT">8L4</td></tr>
            </table>
            <table id="playerKillsTable">
              <tr class="row-background Hibernia">
                <td>Solo</td><td>3</td><td>0</td><td>10</td><td>1,513</td><td>1523</td>
              </tr>
            </table>
            """;

        using var httpClient = new HttpClient(new StubHandler(html));
        var client = new BlackthornHeraldClient(httpClient);

        var profile = await client.GetTargetProfileAsync("Teagan", CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal("Teagan Presley", profile.Name);
        Assert.Equal("Tir na Nog Adventurers", profile.Guild);
        Assert.Equal("Nightshade", profile.Class);
        Assert.Equal(50, profile.Level);
        Assert.Equal("8L4", profile.RealmRank);
        Assert.Equal(1523, profile.SoloKills);
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });
        }
    }
}
