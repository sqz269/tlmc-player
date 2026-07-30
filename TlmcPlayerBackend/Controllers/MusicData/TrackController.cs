using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/music/track")]
public class TrackController(ITrackRepo trackRepo, ISimilarityRepo similarityRepo) : ControllerBase
{
    private readonly ITrackRepo _trackRepo = trackRepo;
    private readonly ISimilarityRepo _similarityRepo = similarityRepo;

    [HttpGet("{id}")]
    public async Task<ActionResult<TrackReadDto>> GetTrack(TrackId id)
    {
        var track = await _trackRepo.GetTrack(id);
        return track == null ? NotFound() : track;
    }

    [HttpGet("{id}/lyrics")]
    public async Task<ActionResult<LyricsReadDto>> GetLyrics(TrackId id)
    {
        var lyrics = await _trackRepo.GetLyrics(id);
        return lyrics == null ? NotFound() : lyrics;
    }

    [HttpGet("filter")]
    public async Task<ActionResult<CursorPage<TrackWithContext>>> Filter(
        [FromQuery(Name = "circle_id")] List<CircleId>? circleIds,
        [FromQuery(Name = "original_work_id")] List<OriginalWorkId>? originalWorkIds,
        [FromQuery(Name = "original_song_id")] List<OriginalSongId>? originalSongIds,
        [FromQuery(Name = "release_date_from")] DateOnly? releaseDateFrom,
        [FromQuery(Name = "release_date_to")] DateOnly? releaseDateTo,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _trackRepo.Filter(new TrackFilter
        {
            CircleIds = circleIds ?? [],
            OriginalWorkIds = originalWorkIds ?? [],
            OriginalSongIds = originalSongIds ?? [],
            ReleaseDateFrom = releaseDateFrom,
            ReleaseDateTo = releaseDateTo,
        }, cursor, limit);
    }

    [HttpGet("random")]
    public async Task<ActionResult<List<TrackWithContext>>> GetRandom(
        [FromQuery] int count = 20,
        [FromQuery] string? seed = null)
    {
        count = Math.Clamp(count, 1, 100);
        return await _trackRepo.GetRandom(count, seed);
    }

    /// <summary>String-identity credit browse: every track credited to exactly this name.</summary>
    [HttpGet("by-credit")]
    public async Task<ActionResult<CursorPage<TrackWithContext>>> GetByCredit(
        [FromQuery] string name,
        [FromQuery] CreditRole? role = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required");
        }

        limit = Math.Clamp(limit, 1, 200);
        return await _trackRepo.GetTracksByCredit(name, role, cursor, limit);
    }

    /// <summary>
    /// The whole library as a precomputed 2D embedding map, parallel arrays with one
    /// entry per playable track. Empty until the ETL layout load has run.
    /// </summary>
    /// <param name="v">Client cache token, unused server-side: vary it to step around
    /// stale CDN entries (the shipped client sends the UTC date).</param>
    [HttpGet("map")]
    public async Task<ActionResult<TrackMapResponseDto>> GetMap([FromQuery] string? v = null)
    {
        var map = await _similarityRepo.GetTrackMap();

        // The empty payload during an ETL wipe-reload window must never be edge-cached
        // for hours — it once was, and the map stayed blank long after the load.
        Response.Headers.CacheControl = map.Count == 0
            ? "no-store"
            : "public, max-age=86400";

        return map;
    }

    [HttpGet("{id}/similar")]
    public async Task<ActionResult<SimilarTracksResponseDto>> GetSimilar(
        TrackId id,
        [FromQuery] int limit = 20,
        [FromQuery] bool diversify = true)
    {
        limit = Math.Clamp(limit, 1, 50);

        // Over-fetch from the precomputed ranks so the diversity re-rank has slack;
        // reading further down similar_track is just more rows of an index scan.
        var overFetch = Math.Min(100, limit * 3);

        var source = "precomputed";
        var candidates = await _similarityRepo.GetPrecomputed(id, overFetch);

        if (candidates.Count == 0)
        {
            // Tracks ingested since the last precompute have no rows — exactly the
            // tracks people look at. The ANN tier answers for them, and the response
            // says so: the fallback's quality is not the precompute's.
            var exists = await _trackRepo.GetTrack(id) != null;
            if (!exists)
            {
                return NotFound();
            }

            source = "approximate";
            try
            {
                candidates = await _similarityRepo.GetApproximate(id, overFetch);
            }
            catch (EmbeddingNotFoundException)
            {
                // A catalogue-only track (no media, no embedding): similarity has
                // nothing to say, which is different from the track not existing.
                return new SimilarTracksResponseDto
                {
                    Items = [],
                    Source = source,
                    Model = await _similarityRepo.GetModel(),
                };
            }
        }

        List<SimilarTrackItemDto> items;
        if (diversify)
        {
            var reranked = DiversityReranker.Rerank(
                candidates.Select(c => new DiversityReranker.Candidate(
                    c.Context.Track.Id,
                    c.Score,
                    c.Context.Release.Id,
                    c.Context.Circles.Select(x => x.Id).ToList())).ToList(),
                limit);

            var byId = candidates.ToDictionary(c => c.Context.Track.Id);
            items = reranked
                .Select(r => new SimilarTrackItemDto
                {
                    Track = byId[r.Candidate.TrackId].Context.Track,
                    Release = byId[r.Candidate.TrackId].Context.Release,
                    Circles = byId[r.Candidate.TrackId].Context.Circles,
                    Score = r.Candidate.Score,
                    Relevance = r.Relevance,
                })
                .ToList();
        }
        else
        {
            var take = candidates.Take(limit).ToList();
            var min = take.Min(c => c.Score);
            var max = take.Max(c => c.Score);
            var span = max - min;
            items = take
                .Select(c => new SimilarTrackItemDto
                {
                    Track = c.Context.Track,
                    Release = c.Context.Release,
                    Circles = c.Context.Circles,
                    Score = c.Score,
                    Relevance = span <= 0 ? 1f : (c.Score - min) / span,
                })
                .ToList();
        }

        return new SimilarTracksResponseDto
        {
            Items = items,
            Source = source,
            Model = await _similarityRepo.GetModel(),
        };
    }
}
