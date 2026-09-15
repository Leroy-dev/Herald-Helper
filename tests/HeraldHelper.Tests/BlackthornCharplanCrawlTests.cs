using System.Text;
using System.Text.Json;
using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class BlackthornCharplanCrawlTests
{
    private static readonly string[] ClassNames =
        "Hero,Warrior,Mage,Cleric,Scout,Ranger,Paladin,Druid,Bard,Hunter,Monk,Shaman,Skald,Thane,Valkyrie,Wizard,Sorcerer,Cabalist,Necromancer,Theurgist,Runemaster,Spiritmaster,Minstrel,Infiltrator,Mercenary,Armswoman,Champion,Blademaster,Warden,Friar".Split(',');

    [Fact]
    public async Task RunAsync_BuildsSnapshotFromNextDataPages()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "data"));

        try
        {
            var output = new StringBuilder();
            var crawl = new FakeBlackthornCrawl(projectRoot, output);
            await crawl.RunAsync(null!, CancellationToken.None);

            var catalog = Path.Combine(projectRoot, "data", "blackthorn-charplan");
            var index = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(catalog, "generated", "classes", "index.json")));
            // ClassNames + the Armsman entry the fake bundle enumerates.
            Assert.Equal(ClassNames.Length + 1, index.RootElement.GetArrayLength());
            Assert.Equal("Armsman", index.RootElement[0].GetProperty("name").GetString());

            var manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(catalog, "generated", "manifest.json")));
            Assert.Equal(ClassNames.Length + 1, manifest.RootElement.GetProperty("classCount").GetInt32());
            Assert.Equal(6, manifest.RootElement.GetProperty("iconAssetCount").GetInt32());

            Assert.True(File.Exists(Path.Combine(catalog, "assets", "icons", "spells", "spl_0.bmp")));
            Assert.True(File.Exists(Path.Combine(catalog, "assets", "icons", "styles", "cbt_500.bmp")));
            Assert.True(File.Exists(Path.Combine(catalog, "generated", "classes", "hero.json")));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    private sealed class FakeBlackthornCrawl : BlackthornCharplanCrawl
    {
        public FakeBlackthornCrawl(string projectRoot, StringBuilder output)
            : base(projectRoot, output, null)
        {
        }

        protected override Task<string> FetchTextAsync(System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                var entries = string.Join("", ClassNames.Select((n, i) => $"e[e.{n}={i + 1}]=\"{n}\";"));
                return Task.FromResult($"var x={{}};e[e.Armsman=1]=\"Armsman\";{entries}}}({{}})");
            }
            if (path.StartsWith("/class/", StringComparison.Ordinal))
            {
                var name = Uri.UnescapeDataString(path["/class/".Length..]);
                var bundleLink = "<script src=\"/_next/static/pages/class/%5Bclass%5D-abc123.js\"></script>";
                var nextData = $"<script id=\"__NEXT_DATA__\" type=\"application/json\">{BuildNextData(name)}</script>";
                return Task.FromResult($"<html><head>{bundleLink}</head><body>{nextData}</body></html>");
            }
            throw new InvalidOperationException($"Unexpected URL: {url}");
        }

        protected override Task<byte[]?> FetchOptionalBinaryAsync(System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            var path = new Uri(url).AbsolutePath;
            if (path.StartsWith("/icons/", StringComparison.Ordinal))
            {
                return Task.FromResult<byte[]?>(new byte[2000]);
            }
            return Task.FromResult<byte[]?>(null);
        }

        private static string BuildNextData(string name)
        {
            // Six icon-bearing nodes -> six distinct sheets (5 minimum required).
            var classData = $$$"""
            {
              "name": "{{{name}}}",
              "classId": {{{Math.Max(0, ClassNames.ToList().IndexOf(name)) + 10}}},
              "realm": "Albion",
              "specs": [{"id":1,"name":"Spec","skills":[]}],
              "realmAbilities": [],
              "abilities": [
                {"objectType":"Spell","icon":{"iconLocation":0}},
                {"objectType":"Spell","icon":{"iconLocation":100}},
                {"objectType":"Spell","icon":{"iconLocation":200}},
                {"objectType":"Style","icon":{"iconLocation":500}},
                {"objectType":"Style","icon":{"iconLocation":600}},
                {"objectType":"Style","icon":{"iconLocation":700}}
              ]
            }
            """;
            return $$$"""{"props":{"pageProps":{"classData":{{{classData}}}}},"buildId":"b1"}""";
        }
    }
}
