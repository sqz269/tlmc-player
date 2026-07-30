using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TlmcPlayerBackend.Search;

/// <summary>The engine is unreachable (down, DNS, timeout). Callers map this to a
/// clean 503 for search and a logged no-op for index pushes.</summary>
public class MeiliUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>The engine answered, but with an error (bad settings, failed task…).
/// Unlike unavailability this is a bug in what we sent, so it propagates.</summary>
public class MeiliApiException(string message, string? code = null)
    : Exception(message)
{
    public string? Code { get; } = code;
}

public class MeiliEnqueuedTask
{
    [JsonProperty("taskUid")]
    public long TaskUid { get; set; }
}

public class MeiliTask
{
    [JsonProperty("uid")]
    public long Uid { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("error")]
    public MeiliError? Error { get; set; }

    public bool IsTerminal => Status is "succeeded" or "failed" or "canceled";
}

public class MeiliError
{
    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }
}

public class MeiliSearchRequest
{
    [JsonProperty("q")]
    public string Q { get; set; } = null!;

    [JsonProperty("limit")]
    public int Limit { get; set; }

    [JsonProperty("offset")]
    public int Offset { get; set; }

    [JsonProperty("attributesToRetrieve")]
    public string[] AttributesToRetrieve { get; set; } = ["*"];

    [JsonProperty("attributesToHighlight")]
    public string[]? AttributesToHighlight { get; set; }

    [JsonProperty("highlightPreTag")]
    public string HighlightPreTag { get; set; } = "<em>";

    [JsonProperty("highlightPostTag")]
    public string HighlightPostTag { get; set; } = "</em>";

    /// <summary>Query-side locale hint; takes precedence over detection, which
    /// matters for short pure-kanji queries exactly as it does for documents.</summary>
    [JsonProperty("locales", NullValueHandling = NullValueHandling.Ignore)]
    public string[]? Locales { get; set; }
}

public class MeiliSearchResult
{
    [JsonProperty("hits")]
    public List<JObject> Hits { get; set; } = [];

    [JsonProperty("estimatedTotalHits")]
    public long EstimatedTotalHits { get; set; }

    [JsonProperty("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// A deliberately small client for the documented Meilisearch REST API.
///
/// Not the official SDK, on purpose: as of Meilisearch .NET 0.20.0 neither the
/// <c>localizedAttributes</c> index setting nor the <c>locales</c> search parameter
/// exists in its typed surface, and those two are precisely the CJK correctness
/// fixes SCHEMA-V6.md section 7 calls load-bearing. The five endpoints this class
/// wraps are stable public API; taking a dependency that cannot express the one
/// setting the design hinges on bought nothing.
///
/// Protocol JSON is camelCase (explicit <c>JsonProperty</c> names); documents carry
/// their own explicit snake_case names. Neither depends on MVC's serializer config.
/// </summary>
public class MeiliClient(HttpClient http)
{
    private static readonly JsonSerializerSettings Json = new()
    {
        NullValueHandling = NullValueHandling.Include,
    };

    private readonly HttpClient _http = http;

    public async Task<MeiliEnqueuedTask> CreateIndexAsync(string uid, string primaryKey, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Post, "indexes", new { uid, primaryKey }, ct);
    }

    public async Task<MeiliEnqueuedTask> DeleteIndexAsync(string uid, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Delete, $"indexes/{uid}", null, ct);
    }

    public async Task<MeiliEnqueuedTask> PatchSettingsAsync(string uid, object settings, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Patch, $"indexes/{uid}/settings", settings, ct);
    }

    /// <summary>Add-or-replace by primary key. The projection always produces the
    /// whole document, so replace semantics are the correct ones.</summary>
    public async Task<MeiliEnqueuedTask> PutDocumentsAsync<T>(string uid, IReadOnlyCollection<T> documents, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Put, $"indexes/{uid}/documents", documents, ct);
    }

    public async Task<MeiliEnqueuedTask> DeleteDocumentsAsync(string uid, IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Post, $"indexes/{uid}/documents/delete-batch", ids, ct);
    }

    public async Task<MeiliEnqueuedTask> SwapIndexesAsync(string a, string b, CancellationToken ct)
    {
        return await SendAsync<MeiliEnqueuedTask>(
            HttpMethod.Post, "swap-indexes", new object[] { new { indexes = new[] { a, b } } }, ct);
    }

    public async Task<MeiliSearchResult> SearchAsync(string uid, MeiliSearchRequest request, CancellationToken ct)
    {
        return await SendAsync<MeiliSearchResult>(
            HttpMethod.Post, $"indexes/{uid}/search", request, ct);
    }

    /// <summary>
    /// Polls the enqueued task to a terminal state. A failed task throws
    /// <see cref="MeiliApiException"/> unless its error code is
    /// <paramref name="tolerateErrorCode"/> (e.g. index_already_exists on the
    /// idempotent create path).
    /// </summary>
    public async Task WaitForTaskAsync(
        MeiliEnqueuedTask enqueued, CancellationToken ct,
        string? tolerateErrorCode = null, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(10));
        var delay = TimeSpan.FromMilliseconds(200);

        while (true)
        {
            var task = await SendAsync<MeiliTask>(
                HttpMethod.Get, $"tasks/{enqueued.TaskUid}", null, ct);

            if (task.IsTerminal)
            {
                if (task.Status == "succeeded")
                {
                    return;
                }

                if (task.Error?.Code is { } code && code == tolerateErrorCode)
                {
                    return;
                }

                throw new MeiliApiException(
                    $"Meilisearch task {task.Uid} ({task.Type}) {task.Status}: {task.Error?.Message}",
                    task.Error?.Code);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new MeiliUnavailableException(
                    $"Meilisearch task {enqueued.TaskUid} did not finish within the wait budget");
            }

            await Task.Delay(delay, ct);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 2000));
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = new StringContent(
                JsonConvert.SerializeObject(body, Json), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException e)
        {
            throw new MeiliUnavailableException($"Meilisearch is unreachable: {e.Message}", e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new MeiliUnavailableException("Meilisearch timed out", e);
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = TryParseError(payload);
                throw new MeiliApiException(
                    $"Meilisearch {(int)response.StatusCode} on {method} {path}: {error?.Message ?? payload}",
                    error?.Code);
            }

            return JsonConvert.DeserializeObject<T>(payload, Json)
                   ?? throw new MeiliApiException($"Empty Meilisearch response on {method} {path}");
        }
    }

    private static MeiliError? TryParseError(string payload)
    {
        try
        {
            return JsonConvert.DeserializeObject<MeiliError>(payload, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
