using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Search;

public class TrackSearchHit
{
    public TrackId Id { get; set; }

    /// <summary>Attribute → highlighted fragment(s), only for attributes the query
    /// actually matched. Keys are document field names, values string or string[].</summary>
    public Dictionary<string, object> Highlights { get; set; } = [];
}

public class TrackSearchResult
{
    public List<TrackSearchHit> Hits { get; set; } = [];
    public long EstimatedTotalHits { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public interface ISearchIndexService
{
    bool Enabled { get; }

    /// <summary>Creates the index and applies the settings contract. Idempotent.</summary>
    Task EnsureIndexAsync(CancellationToken ct);

    /// <summary>
    /// since == null: full rebuild into a staging index, then an atomic swap — a
    /// stale document for a deleted track cannot survive it. since != null:
    /// incremental upsert of tracks with updated_at past the watermark, straight
    /// into the live index. Returns the number of documents written.
    /// </summary>
    Task<int> ReindexAsync(DateTime? since, CancellationToken ct);

    /// <summary>Best-effort push after a mutation. Never throws: the write already
    /// committed and must not fail because the engine is down — the bumped
    /// updated_at watermark lets the next incremental reindex repair the miss.</summary>
    Task<bool> TryIndexTracksAsync(IReadOnlyCollection<TrackId> ids, CancellationToken ct);

    /// <summary>Engine query returning ids plus highlight fragments; hydration
    /// stays in Postgres (SCHEMA-V6.md section 15).</summary>
    Task<TrackSearchResult> SearchAsync(
        string query, int limit, int offset, string[]? locales, CancellationToken ct);
}

public class SearchIndexService(
    MeiliClient meili,
    AppDbContext context,
    IOptions<SearchOptions> options,
    ILogger<SearchIndexService> logger) : ISearchIndexService
{
    private const int BatchSize = 1000;

    private readonly MeiliClient _meili = meili;
    private readonly AppDbContext _context = context;
    private readonly SearchOptions _options = options.Value;
    private readonly ILogger<SearchIndexService> _logger = logger;

    public bool Enabled => _options.Enabled;

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        await EnsureIndexAsync(_options.IndexUid, ct);
    }

    public async Task<int> ReindexAsync(DateTime? since, CancellationToken ct)
    {
        return since == null
            ? await RebuildAsync(ct)
            : await IndexBatchesAsync(_options.IndexUid, since, ct);
    }

    public async Task<bool> TryIndexTracksAsync(IReadOnlyCollection<TrackId> ids, CancellationToken ct)
    {
        if (!Enabled || ids.Count == 0)
        {
            return false;
        }

        try
        {
            var documents = await TrackSearchProjection.ProjectAsync(
                _context.Tracks.Where(t => ids.Contains(t.Id)), ct);
            var task = await _meili.PutDocumentsAsync(_options.IndexUid, documents, ct);
            await _meili.WaitForTaskAsync(task, ct, timeout: TimeSpan.FromMinutes(1));
            return true;
        }
        catch (Exception e) when (e is MeiliUnavailableException or MeiliApiException)
        {
            _logger.LogWarning(e,
                "Search push for {Count} track(s) failed; the updated_at watermark will catch it on the next reindex",
                ids.Count);
            return false;
        }
    }

    public async Task<TrackSearchResult> SearchAsync(
        string query, int limit, int offset, string[]? locales, CancellationToken ct)
    {
        var result = await _meili.SearchAsync(_options.IndexUid, new MeiliSearchRequest
        {
            Q = query,
            Limit = limit,
            Offset = offset,
            AttributesToRetrieve = ["id"],
            AttributesToHighlight = ["*"],
            Locales = locales is { Length: > 0 } ? locales : null,
        }, ct);

        var hits = new List<TrackSearchHit>(result.Hits.Count);
        foreach (var hit in result.Hits)
        {
            if (!TrackId.TryParse(hit.Value<string>("id"), null, out var id))
            {
                continue;
            }

            hits.Add(new TrackSearchHit
            {
                Id = id,
                Highlights = ExtractHighlights(hit["_formatted"] as JObject),
            });
        }

        return new TrackSearchResult
        {
            Hits = hits,
            EstimatedTotalHits = result.EstimatedTotalHits,
            ProcessingTimeMs = result.ProcessingTimeMs,
        };
    }

    private async Task EnsureIndexAsync(string uid, CancellationToken ct)
    {
        var create = await _meili.CreateIndexAsync(uid, SearchIndexContract.PrimaryKey, ct);
        await _meili.WaitForTaskAsync(create, ct, tolerateErrorCode: "index_already_exists");

        var settings = await _meili.PatchSettingsAsync(uid, SearchIndexContract.BuildSettings(), ct);
        await _meili.WaitForTaskAsync(settings, ct);
    }

    private async Task<int> RebuildAsync(CancellationToken ct)
    {
        var live = _options.IndexUid;
        var staging = live + "_rebuild";

        // The live index must exist for the swap; a leftover staging index from a
        // crashed rebuild is discarded rather than resumed.
        await EnsureIndexAsync(live, ct);
        var stale = await _meili.DeleteIndexAsync(staging, ct);
        await _meili.WaitForTaskAsync(stale, ct, tolerateErrorCode: "index_not_found");
        await EnsureIndexAsync(staging, ct);

        var count = await IndexBatchesAsync(staging, since: null, ct);

        var swap = await _meili.SwapIndexesAsync(live, staging, ct);
        await _meili.WaitForTaskAsync(swap, ct);

        // Post-swap the staging uid holds the previous generation of documents.
        var cleanup = await _meili.DeleteIndexAsync(staging, ct);
        await _meili.WaitForTaskAsync(cleanup, ct);

        return count;
    }

    private async Task<int> IndexBatchesAsync(string uid, DateTime? since, CancellationToken ct)
    {
        var total = 0;
        var lastId = Guid.Empty;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Keyset batching over the id (never Skip/Take — the loader already
            // paid for that lesson); updated_at rides track_updated_at_idx.
            var ids = await _context.Database.SqlQuery<Guid>($@"
                SELECT t.id AS ""Value""
                FROM track t
                WHERE ({since}::timestamptz IS NULL OR t.updated_at > {since})
                  AND t.id > {lastId}
                ORDER BY t.id
                LIMIT {BatchSize}")
                .ToListAsync(ct);

            if (ids.Count == 0)
            {
                return total;
            }

            var typedIds = ids.Select(g => new TrackId(g)).ToList();
            var documents = await TrackSearchProjection.ProjectAsync(
                _context.Tracks.Where(t => typedIds.Contains(t.Id)), ct);

            var task = await _meili.PutDocumentsAsync(uid, documents, ct);
            await _meili.WaitForTaskAsync(task, ct);

            total += documents.Count;
            lastId = ids[^1];
            _logger.LogInformation("Search reindex: {Total} documents into {Uid}", total, uid);
        }
    }

    private static Dictionary<string, object> ExtractHighlights(JObject? formatted)
    {
        // _formatted echoes every highlighted attribute whether or not it matched;
        // only fragments that actually contain a highlight are worth the payload.
        var highlights = new Dictionary<string, object>();
        if (formatted == null)
        {
            return highlights;
        }

        foreach (var property in formatted.Properties())
        {
            if (property.Name == "id")
            {
                continue;
            }

            switch (property.Value)
            {
                case JValue { Type: JTokenType.String } value
                    when IsHighlighted((string?)value):
                    highlights[property.Name] = (string)value!;
                    break;

                case JArray array:
                    var matched = array
                        .OfType<JValue>()
                        .Where(v => v.Type == JTokenType.String && IsHighlighted((string?)v))
                        .Select(v => (string)v!)
                        .ToList();
                    if (matched.Count > 0)
                    {
                        highlights[property.Name] = matched;
                    }

                    break;
            }
        }

        return highlights;
    }

    private static bool IsHighlighted(string? fragment)
    {
        return fragment != null && fragment.Contains("<em>", StringComparison.Ordinal);
    }
}
