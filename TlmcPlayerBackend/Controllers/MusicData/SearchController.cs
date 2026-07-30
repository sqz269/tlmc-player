using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Search;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// Full-text search over the track index (SCHEMA-V6.md section 7). The engine
/// answers with ids and highlight fragments; everything a client renders is
/// hydrated from Postgres so search results and every other list endpoint share
/// one shape. When the engine is down this controller answers 503 — browse,
/// playback and playlists never depend on it.
/// </summary>
[ApiController]
[Route("api/search")]
public class SearchController(ISearchIndexService search, ITrackRepo trackRepo) : ControllerBase
{
    private readonly ISearchIndexService _search = search;
    private readonly ITrackRepo _trackRepo = trackRepo;

    [HttpGet]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        [FromQuery] List<string>? locales = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("q is required");
        }

        limit = Math.Clamp(limit, 1, 50);
        offset = Math.Clamp(offset, 0, 1000);

        // Optional script hint from a client that knows what the user typed
        // (e.g. an IME-aware input): forwarded as Meilisearch `locales`, which
        // overrides detection — short pure-kanji queries are otherwise guessed
        // as Mandarin and segmented wrong.
        var localeHints = locales?.Distinct().ToArray() ?? [];
        var invalid = localeHints.Except(SearchIndexContract.AllowedQueryLocales).ToList();
        if (invalid.Count > 0)
        {
            return BadRequest(
                $"unsupported locales: {string.Join(", ", invalid)} " +
                $"(supported: {string.Join(", ", SearchIndexContract.AllowedQueryLocales)})");
        }

        if (!_search.Enabled)
        {
            return Problem(
                title: "Search is not available",
                detail: "No search engine is configured for this deployment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        TrackSearchResult result;
        try
        {
            result = await _search.SearchAsync(q, limit, offset, localeHints, ct);
        }
        catch (MeiliUnavailableException)
        {
            return Problem(
                title: "Search is not available",
                detail: "The search engine did not respond.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var hydrated = await _trackRepo.GetWithContext(result.Hits.Select(h => h.Id).ToList());
        var byId = result.Hits.ToDictionary(h => h.Id);

        return new SearchResponseDto
        {
            Query = q,
            Items = hydrated
                .Select(t => new SearchHitDto
                {
                    Track = t.Track,
                    Release = t.Release,
                    Circles = t.Circles,
                    ArtworkId = t.ArtworkId,
                    Highlights = byId[t.Track.Id].Highlights,
                })
                .ToList(),
            EstimatedTotalHits = result.EstimatedTotalHits,
            Limit = limit,
            Offset = offset,
            ProcessingTimeMs = result.ProcessingTimeMs,
        };
    }
}
