using System.Text;

namespace HeraldHelper.Desktop;

internal interface ICatalogCrawler
{
    Task RunAsync(
        string catalog,
        StringBuilder output,
        CancellationToken cancellationToken);
}
