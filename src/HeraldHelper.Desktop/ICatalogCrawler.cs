using System.Text;

namespace HeraldHelper.Desktop;

internal interface ICatalogCrawler
{
    Task RunAsync(
        string catalog,
        string projectRoot,
        StringBuilder output,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
