using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Dtos.MusicData.Lyrics;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/music")]
public class TrackController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IMapper _mapper;


    public TrackController(
        IAlbumRepo albumRepo,
        ITrackRepo trackRepo,
        IOriginalTrackRepo originalTrackRepo,
        IMapper mapper)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;

    }

    [HttpGet("track/{id:Guid}", Name = nameof(GetTrack))]
    [ProducesResponseType(typeof(TrackReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrack(Guid id)
    {
        var track = await _trackRepo.GetTrack(id);
        if (track == null)
            return NotFound();
        var mapped = _mapper.Map<Track, TrackReadDto>(track);
        return Ok(mapped);
    }

    [HttpGet("track/{trackId:Guid}/lyrics", Name = nameof(GetLyrics))]
    [ProducesResponseType(typeof(LyricsReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    public async Task<ActionResult<LyricsReadDto>> GetLyrics(Guid trackId)
    {
        var lyrics = await _trackRepo.GetTrackLyrics(trackId);
        if (lyrics == null)
        {
            return NotFound();
        }

        return Ok(_mapper.Map<Lyrics, LyricsReadDto>(lyrics));
    }

    [HttpPost("track", Name = nameof(GetTracks))]
    [ProducesResponseType(typeof(TrackGetMultipleResp), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTracks([FromBody] IEnumerable<Guid> trackIds)
    {
        var list = trackIds.ToList();
        var result = await _trackRepo.GetTracks(list);
        return Ok(new TrackGetMultipleResp
        {
            NotFound = result.Item2,
            Tracks = _mapper.Map<IEnumerable<Track>, IEnumerable<TrackReadDto>>(result.Item1)
        });
    }

    [HttpGet("track/filter", Name = nameof(GetTracksFiltered))]
    [ProducesResponseType(typeof(TrackListResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackListResult>> GetTracksFiltered(
        [FromQuery] TrackFilterSelectableRanged filter,
        [FromQuery] [Range(0, int.MaxValue)] int start = 0,
        [FromQuery][Range(1, 50)] int limit = 20,
        [FromQuery] TrackOrderOptions sort = TrackOrderOptions.Id,
        [FromQuery] SortOrder sortOrder = SortOrder.Ascending)
    {
        var (tracks, count) = await _trackRepo.GetTracksFiltered(filter, limit, start, sort, sortOrder);

        var trackDto = _mapper.Map<IEnumerable<TrackReadDto>>(tracks);

        return Ok(new TrackListResult
        {
            Tracks = trackDto,
            Count = trackDto.Count(),
            Total = count
        });
    }

    [HttpGet("track/{trackId:guid}/similar", Name = nameof(GetSimilarTracksDiverse))]
    [ProducesResponseType(typeof(TrackListResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackSimilaritiesResult>> GetSimilarTracksDiverse(
        Guid trackId,
        [FromQuery] TrackEmbeddingPoolingMode poolingMode = TrackEmbeddingPoolingMode.MeanMax,
        [FromQuery, Range(1, 50)] int limit = 20,
        [FromQuery] TrackSimilarityRankingPenaltyAttribute penaltyAttributes = TrackSimilarityRankingPenaltyAttribute.Title | TrackSimilarityRankingPenaltyAttribute.Artist | TrackSimilarityRankingPenaltyAttribute.CosineDistThreshold,
        [FromQuery][Range(0.0, 1.0)] double relevanceWeight = 0.5, // lambda
        [FromQuery][Range(0.0, 1.0)] double cosineDistThreshold = 0.0001
        )
    {
        var overfetchLimit = Math.Min(300, limit * 7);

        IEnumerable<(Track Track, double Distance)> tracks;
        try
        {
            tracks = await _trackRepo.GetSimilarTracks(trackId, overfetchLimit, poolingMode);
        }
        catch (EmbeddingNotFoundException)
        {
            // Embeddings come from a separate inference pipeline that lags ingestion,
            // so a track without one is an expected state rather than a server fault.
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "No Embedding",
                detail: $"Track {trackId} has no embedding, so similar tracks cannot be computed.");
        }

        // Diversity Re-ranking
        var resampled = DiversitySampler.ApplyDiversitySamplerAndRerank(tracks, limit, relevanceWeight, penaltyAttributes, cosineDistThreshold);

        var simResults = resampled.Select(t => new TrackSimilarity
        {
            Track = _mapper.Map<TrackReadDto>(t.track),
            SimilarityScore = Math.Round(1 - t.distance, 4)
        }).ToList();

        return Ok(new TrackSimilaritiesResult
        {
            Items = simResults,
            Count = simResults.Count,
        });
    }

    [HttpGet("random", Name = nameof(GetRandomSampleTrack))]
    [ProducesResponseType(typeof(TrackRandomResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackRandomResult>> GetRandomSampleTrack(
        [FromQuery] [Range(0, int.MaxValue)] int start = 0,
        [FromQuery][Range(1, 50)] int limit = 20,
        [FromQuery] TrackStratificationMode? stratificationMode = null,
        [FromQuery] TrackFilterSelectableRanged? filters = null,
        [FromQuery] string? seed = null)
    {
        var seedValue = SeedUtils.GetSeed(seed);

        var tracks =
            _mapper.Map<List<TrackReadDto>>(await _trackRepo.SampleRandomTrack(limit, start, stratificationMode, filters, seedValue));
        var tracksCount = tracks.Count;
        var trackTotalForFilter = _trackRepo.GetNumberOfTracksGivenFilter(filters);

        return Ok(new TrackRandomResult
        {
            Tracks = tracks,
            Count = tracksCount,
            Total = await trackTotalForFilter,
            Seed = SeedUtils.GetSeedString(seed, seedValue)
        });
    }
}