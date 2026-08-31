using System.Diagnostics;
using System.IO;
using System.Text;

namespace HeraldHelper.Desktop;

internal sealed class NodeCatalogCrawler : ICatalogCrawler
{
    public async Task RunAsync(
        string catalog,
        StringBuilder output,
        CancellationToken cancellationToken)
    {
        var script = catalog switch
        {
            "eden-charplan" => "scripts/crawl_eden_charplan.js",
            "blackthorn-charplan" => "scripts/crawl_blackthorn_charplan.js",
            _ => throw new ArgumentException($"Unknown catalog: {catalog}", nameof(catalog))
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = FindProjectRoot() ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(script);
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            output.AppendLine(await stdout);
            var error = await stderr;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{script} failed: {error.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                output.AppendLine(error.Trim());
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
    }

    private static string? FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "scripts")) &&
                Directory.Exists(Path.Combine(current.FullName, "data")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }
}
