using System.Text;
using System.Text.Json;
using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class EdenCharplanCrawlTests
{
    /// <summary>
    /// Feeds the checked-in raw assets back through the crawl and asserts the
    /// generated dataset matches the on-disk snapshot byte for byte — proof the
    /// C# port is a faithful replacement for the Node script.
    /// </summary>
    [Fact]
    public async Task RunAsync_ReproducesExistingSnapshot()
    {
        var repoRoot = FindRepoRoot();
        var liveCatalog = Path.Combine(repoRoot, "data", "eden-charplan");
        var projectRoot = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "data"));

        try
        {
            var output = new StringBuilder();
            var crawl = new FileBackedEdenCrawl(projectRoot, liveCatalog, output);
            await crawl.RunAsync(null!, CancellationToken.None);

            var staged = Path.Combine(projectRoot, "data", "eden-charplan");
            Assert.True(Directory.Exists(staged), "staged snapshot was not published");

            var liveGenerated = Path.Combine(liveCatalog, "generated");
            var stagedGenerated = Path.Combine(staged, "generated");
            var liveFiles = Directory.EnumerateFiles(liveGenerated, "*.json", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(liveGenerated, p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            var stagedFiles = Directory.EnumerateFiles(stagedGenerated, "*.json", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(stagedGenerated, p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(liveFiles, stagedFiles);

            foreach (var rel in liveFiles)
            {
                var liveText = File.ReadAllText(Path.Combine(liveGenerated, rel));
                var stagedText = File.ReadAllText(Path.Combine(stagedGenerated, rel));
                if (rel == "manifest.json")
                {
                    // generatedAt legitimately differs per run.
                    using var liveDoc = JsonDocument.Parse(liveText);
                    using var stagedDoc = JsonDocument.Parse(stagedText);
                    foreach (var prop in liveDoc.RootElement.EnumerateObject())
                    {
                        if (prop.Name == "generatedAt") continue;
                        Assert.True(stagedDoc.RootElement.TryGetProperty(prop.Name, out var stagedProp),
                            $"manifest missing {prop.Name}");
                        Assert.Equal(prop.Value.GetRawText(), stagedProp.GetRawText());
                    }
                    continue;
                }
                Assert.Equal(liveText, stagedText);
            }

            var stagedRaw = Directory.EnumerateFiles(Path.Combine(staged, "raw"))
                .Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Equal(
                Directory.EnumerateFiles(Path.Combine(liveCatalog, "raw")).Select(Path.GetFileName).OrderBy(n => n).ToList(),
                stagedRaw);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeraldHelper.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate the repo root.");
    }

    private sealed class FileBackedEdenCrawl : EdenCharplanCrawl
    {
        private readonly string _liveCatalog;

        public FileBackedEdenCrawl(string projectRoot, string liveCatalog, StringBuilder output)
            : base(projectRoot, output, null)
        {
            _liveCatalog = liveCatalog;
        }

        protected override Task<string> FetchTextAsync(System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            var path = new Uri(url).AbsolutePath.TrimStart('/');
            var fileName = Path.GetFileName(path);
            var file = Path.Combine(_liveCatalog, "raw", fileName);
            return Task.FromResult(File.ReadAllText(file));
        }

        protected override Task<byte[]> FetchBinaryAsync(System.Net.Http.HttpClient client, string url, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            var file = Path.Combine(_liveCatalog, "assets", "sprites", fileName);
            return Task.FromResult(File.Exists(file) ? File.ReadAllBytes(file) : new byte[] { 1 });
        }
    }
}
