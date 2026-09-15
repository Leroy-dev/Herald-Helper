using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HeraldHelper.Desktop;

/// <summary>
/// Shared machinery for the charplan snapshot crawls: staged output directory,
/// atomic publish into data/&lt;catalog&gt;, and fetch/write helpers.
/// Mirrors the former Node scripts (scripts/crawl_*.js).
/// </summary>
internal abstract class CharplanCrawl
{
    protected static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected CharplanCrawl(string projectRoot, string catalog, StringBuilder output, IProgress<string>? progress)
    {
        Output = output;
        Progress = progress;
        FinalRoot = Path.Combine(projectRoot, "data", catalog);
        StagingRoot = FinalRoot + ".staging-" + Environment.ProcessId;
    }

    protected string FinalRoot { get; }
    protected string StagingRoot { get; }
    protected StringBuilder Output { get; }
    private IProgress<string>? Progress { get; }

    public abstract Task RunAsync(HttpClient client, CancellationToken cancellationToken);

    protected void Report(string message)
    {
        Output.AppendLine(message);
        Progress?.Report(message);
    }

    protected static async Task<string> FetchText(
        HttpClient client, string url, string userAgent, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to fetch {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    protected static async Task<byte[]> FetchBinary(
        HttpClient client, string url, string userAgent, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to fetch {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    protected static async Task<byte[]?> FetchOptionalBinary(
        HttpClient client, string url, string userAgent, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to fetch {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    protected static void WriteJson(string path, JsonNode? value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = (NormalizeJsonNumbers(value) ?? JsonValue.Create((string?)null)!).ToJsonString(IndentedJson);
        File.WriteAllText(path, json.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// JS JSON.stringify writes the shortest round-trip form (1.0 -> 1);
    /// parsed JsonElement nodes keep their raw text, so normalize numbers here
    /// to reproduce the Node script's output byte for byte.
    /// </summary>
    private static JsonNode? NormalizeJsonNumbers(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                var normalizedObj = new JsonObject();
                foreach (var (key, child) in obj)
                {
                    normalizedObj[key] = NormalizeJsonNumbers(child);
                }
                return normalizedObj;
            case JsonArray array:
                var normalizedArr = new JsonArray();
                foreach (var child in array)
                {
                    normalizedArr.Add(NormalizeJsonNumbers(child));
                }
                return normalizedArr;
            case JsonValue value:
                if (value.TryGetValue<JsonElement>(out var element) &&
                    element.ValueKind == JsonValueKind.Number)
                {
                    var raw = element.GetRawText();
                    if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
                    {
                        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var d))
                        {
                            if (d == Math.Truncate(d) && Math.Abs(d) < 9007199254740992.0)
                            {
                                return JsonValue.Create(d == 0 ? 0L : (long)d);
                            }
                            return JsonValue.Create(d);
                        }
                    }
                }
                return value.DeepClone();
            default:
                return node.DeepClone();
        }
    }

    protected static string Slugify(string value)
    {
        var lowered = value.ToLowerInvariant();
        var dashed = Regex.Replace(lowered, "[^a-z0-9]+", "-");
        return dashed.Trim('-');
    }

    /// <summary>JS truthiness: null, false, 0 and "" are falsy.</summary>
    protected static bool IsTruthy(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return false;
            case JsonValue value:
                if (value.TryGetValue<bool>(out var b)) return b;
                if (value.TryGetValue<double>(out var d)) return d != 0;
                if (value.TryGetValue<string>(out var s)) return s.Length != 0;
                return true;
            default:
                return true;
        }
    }

    protected static JsonNode? NullIfFalsy(JsonNode? node)
    {
        return IsTruthy(node) ? node : null;
    }

    /// <summary>
    /// Moves the staging directory into place atomically: the live catalog is
    /// renamed aside first so a failed move leaves the previous snapshot intact.
    /// </summary>
    protected void PublishSnapshot()
    {
        var backupRoot = FinalRoot + ".backup-" + Environment.ProcessId;
        if (Directory.Exists(backupRoot))
        {
            Directory.Delete(backupRoot, recursive: true);
        }

        var movedCurrent = false;
        try
        {
            if (Directory.Exists(FinalRoot))
            {
                Directory.Move(FinalRoot, backupRoot);
                movedCurrent = true;
            }
            Directory.Move(StagingRoot, FinalRoot);
            if (Directory.Exists(backupRoot))
            {
                Directory.Delete(backupRoot, recursive: true);
            }
        }
        catch
        {
            if (movedCurrent && !Directory.Exists(FinalRoot) && Directory.Exists(backupRoot))
            {
                Directory.Move(backupRoot, FinalRoot);
            }
            throw;
        }
    }

    protected void DeleteStaging()
    {
        if (Directory.Exists(StagingRoot))
        {
            Directory.Delete(StagingRoot, recursive: true);
        }
    }
}
