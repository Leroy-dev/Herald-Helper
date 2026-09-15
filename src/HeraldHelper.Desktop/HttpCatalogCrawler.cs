using System.Net.Http;
using System.Text;

namespace HeraldHelper.Desktop;

/// <summary>
/// In-process catalog crawler: replaces the Node.js scripts so the app has no
/// external runtime dependency. Each catalog crawl writes a staged snapshot and
/// swaps it into place only after generation succeeds.
/// </summary>
internal sealed class HttpCatalogCrawler : ICatalogCrawler
{
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _client;

    public HttpCatalogCrawler() : this(SharedClient)
    {
    }

    internal HttpCatalogCrawler(HttpClient client)
    {
        _client = client;
    }

    public async Task RunAsync(
        string catalog,
        string projectRoot,
        StringBuilder output,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        CharplanCrawl crawl = catalog switch
        {
            "eden-charplan" => new EdenCharplanCrawl(projectRoot, output, progress),
            "blackthorn-charplan" => new BlackthornCharplanCrawl(projectRoot, output, progress),
            _ => throw new ArgumentException($"Unknown catalog: {catalog}", nameof(catalog))
        };

        await crawl.RunAsync(_client, cancellationToken);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        return client;
    }
}
