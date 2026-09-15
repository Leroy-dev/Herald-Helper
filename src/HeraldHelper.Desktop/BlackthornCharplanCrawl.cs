using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HeraldHelper.Desktop;

/// <summary>
/// C# port of scripts/crawl_blackthorn_charplan.js: discovers class names from
/// the Blackthorn planner bundle, fetches each class page's __NEXT_DATA__
/// payload, downloads referenced icon sheets, then swaps the snapshot into
/// data/blackthorn-charplan atomically.
/// </summary>
internal class BlackthornCharplanCrawl : CharplanCrawl
{
    private const string BaseUrl = "https://blackthorn-daoc.com";
    private const string UserAgent = "HeraldHelper charplan snapshot crawler";
    private const int ClassFetchConcurrency = 5;

    private static readonly Regex BundlePathRegex = new(
        @"src=""([^""]*/pages/class/%5Bclass%5D-[^""]+\.js)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClassNameRegex = new(
        @"e\[e\.[A-Za-z]+\s*=\s*\d+\]\s*=\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex NextDataRegex = new(
        @"<script id=""__NEXT_DATA__"" type=""application/json"">([\s\S]*?)</script>",
        RegexOptions.Compiled);

    public BlackthornCharplanCrawl(string projectRoot, StringBuilder output, IProgress<string>? progress)
        : base(projectRoot, "blackthorn-charplan", output, progress)
    {
    }

    protected virtual Task<string> FetchTextAsync(HttpClient client, string url, CancellationToken cancellationToken)
        => FetchText(client, url, UserAgent, cancellationToken);

    protected virtual Task<byte[]?> FetchOptionalBinaryAsync(HttpClient client, string url, CancellationToken cancellationToken)
        => FetchOptionalBinary(client, url, UserAgent, cancellationToken);

    public override async Task RunAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var classDir = Path.Combine(StagingRoot, "generated", "classes");
        var iconRoot = Path.Combine(StagingRoot, "assets", "icons");

        DeleteStaging();
        Directory.CreateDirectory(classDir);

        try
        {
            Report("Discovering Blackthorn class list");
            var classNames = await DiscoverClassNamesAsync(client, cancellationToken);
            Report($"Found {classNames.Count} classes");

            var fetched = await MapWithConcurrencyAsync(classNames, ClassFetchConcurrency,
                name => FetchClassDataAsync(client, name, cancellationToken), cancellationToken);
            Report($"Fetched {fetched.Count} class pages");

            var iconAssets = DiscoverIconAssets(fetched.Select(item => item.ClassData));
            var downloadedIconAssets = new List<string>();
            var missingIconAssets = new List<string>();
            foreach (var asset in iconAssets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = await FetchOptionalBinaryAsync(client, $"{BaseUrl}/icons/{asset}", cancellationToken);
                if (content is null)
                {
                    missingIconAssets.Add(asset);
                    continue;
                }
                if (content.Length < 1000)
                {
                    throw new InvalidOperationException($"Blackthorn icon asset is unexpectedly small: {asset}");
                }
                var targetPath = Path.Combine(iconRoot, asset);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.WriteAllBytes(targetPath, content);
                downloadedIconAssets.Add(asset);
            }
            Report($"Downloaded {downloadedIconAssets.Count} icon assets ({missingIconAssets.Count} missing upstream)");
            if (downloadedIconAssets.Count < 5)
            {
                throw new InvalidOperationException(
                    $"Blackthorn snapshot validation found only {downloadedIconAssets.Count} icon assets.");
            }

            var classIndex = new JsonArray();
            var buildIds = new HashSet<string>(StringComparer.Ordinal);
            var indexEntries = new List<JsonObject>();

            foreach (var item in fetched)
            {
                var classData = item.ClassData;
                var slug = Slugify(classData["name"]!.GetValue<string>());
                if (!string.IsNullOrWhiteSpace(item.BuildId))
                {
                    buildIds.Add(item.BuildId);
                }

                WriteJson(Path.Combine(classDir, $"{slug}.json"), classData);
                indexEntries.Add(new JsonObject
                {
                    ["id"] = classData["classId"]?.DeepClone(),
                    ["name"] = classData["name"]?.DeepClone(),
                    ["slug"] = slug,
                    ["realm"] = classData["realm"]?.DeepClone(),
                    ["specCount"] = classData["specs"] is JsonArray specs ? specs.Count : 0,
                    ["realmAbilityCount"] = classData["realmAbilities"] is JsonArray ras ? ras.Count : 0,
                    ["sourceUrl"] = item.SourceUrl
                });
            }

            indexEntries.Sort((a, b) => string.Compare(
                a["name"]?.GetValue<string>(), b["name"]?.GetValue<string>(), StringComparison.Ordinal));
            foreach (var entry in indexEntries)
            {
                classIndex.Add(entry);
            }

            if (indexEntries.Count != classNames.Count ||
                indexEntries.Any(x => !IsTruthy(x["id"]) || !IsTruthy(x["name"]) ||
                    x["specCount"]!.GetValue<int>() == 0))
            {
                throw new InvalidOperationException("Blackthorn snapshot validation failed.");
            }

            WriteJson(Path.Combine(classDir, "index.json"), classIndex);
            WriteJson(Path.Combine(StagingRoot, "generated", "manifest.json"), new JsonObject
            {
                ["source"] = BaseUrl,
                ["generatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                ["buildIds"] = new JsonArray(buildIds.Select(id => (JsonNode)id).ToArray()),
                ["classCount"] = indexEntries.Count,
                ["iconAssetCount"] = downloadedIconAssets.Count,
                ["missingIconAssets"] = new JsonArray(missingIconAssets.Select(a => (JsonNode)a).ToArray()),
                ["files"] = new JsonObject
                {
                    ["classIndex"] = "generated/classes/index.json",
                    ["classDirectory"] = "generated/classes",
                    ["iconDirectory"] = "assets/icons"
                }
            });

            PublishSnapshot();

            Report($"Saved Blackthorn charplan dataset to {FinalRoot}");
            Report($"Classes: {indexEntries.Count}");
            Report($"Icon assets: {downloadedIconAssets.Count} ({missingIconAssets.Count} missing upstream)");
        }
        catch
        {
            DeleteStaging();
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverClassNamesAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var html = await FetchTextAsync(client, $"{BaseUrl}/class/Armsman", cancellationToken);
        var scriptMatch = BundlePathRegex.Match(html);
        if (!scriptMatch.Success)
        {
            throw new InvalidOperationException("Could not find the Blackthorn class planner bundle.");
        }

        var scriptUrl = new Uri(new Uri(BaseUrl), scriptMatch.Groups[1].Value).ToString();
        var script = await FetchTextAsync(client, scriptUrl, cancellationToken);
        var start = script.IndexOf("e[e.Armsman=", StringComparison.Ordinal);
        var end = start < 0 ? -1 : script.IndexOf("}({})", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("Could not find the Blackthorn class enumeration.");
        }

        var classSection = script[start..end];
        var names = ClassNameRegex.Matches(classSection)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();
        if (names.Count < 30)
        {
            throw new InvalidOperationException($"Blackthorn class discovery returned only {names.Count} classes.");
        }
        return names;
    }

    private async Task<FetchedClass> FetchClassDataAsync(HttpClient client, string className, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/class/{Uri.EscapeDataString(className)}";
        var html = await FetchTextAsync(client, url, cancellationToken);
        var match = NextDataRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException($"No __NEXT_DATA__ payload found for {className}.");
        }

        var nextData = JsonNode.Parse(match.Groups[1].Value)!.AsObject();
        var classData = nextData["props"]?["pageProps"]?["classData"] as JsonObject;
        if (classData is null || !IsTruthy(classData["name"]))
        {
            throw new InvalidOperationException($"No classData payload found for {className}.");
        }

        return new FetchedClass(classData, nextData["buildId"]?.GetValue<string>(), url);
    }

    private sealed record FetchedClass(JsonObject ClassData, string? BuildId, string SourceUrl);

    private static async Task<List<TOut>> MapWithConcurrencyAsync<TItem, TOut>(
        IReadOnlyList<TItem> items, int concurrency,
        Func<TItem, Task<TOut>> callback, CancellationToken cancellationToken)
    {
        var results = new TOut[items.Count];
        var nextIndex = -1;

        async Task Worker()
        {
            while (true)
            {
                var index = Interlocked.Increment(ref nextIndex);
                if (index >= items.Count)
                {
                    return;
                }
                results[index] = await callback(items[index]);
            }
        }

        var workers = Enumerable.Range(0, concurrency).Select(_ => Worker()).ToArray();
        await Task.WhenAll(workers);
        cancellationToken.ThrowIfCancellationRequested();
        return results.ToList();
    }

    private static List<string> DiscoverIconAssets(IEnumerable<JsonObject> classDataItems)
    {
        var assets = new HashSet<string>(StringComparer.Ordinal);

        void Visit(JsonNode? value)
        {
            switch (value)
            {
                case JsonObject obj:
                    if ((obj["objectType"]?.GetValue<string>() is "Spell" or "Style") &&
                        obj["icon"] is JsonObject icon &&
                        icon["iconLocation"] is JsonValue locValue &&
                        locValue.TryGetValue<int>(out var loc) && loc >= 0)
                    {
                        var spriteClass = loc / 100 * 100;
                        var directory = obj["objectType"]!.GetValue<string>() == "Spell" ? "spells" : "styles";
                        var prefix = directory == "spells" ? "spl" : "cbt";
                        assets.Add($"{directory}/{prefix}_{spriteClass}.bmp");
                    }
                    foreach (var child in obj)
                    {
                        Visit(child.Value);
                    }
                    break;
                case JsonArray array:
                    foreach (var child in array)
                    {
                        Visit(child);
                    }
                    break;
            }
        }

        foreach (var classData in classDataItems)
        {
            Visit(classData);
        }
        return assets.OrderBy(a => a, StringComparer.Ordinal).ToList();
    }
}
