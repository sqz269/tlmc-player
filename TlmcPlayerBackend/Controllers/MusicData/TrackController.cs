using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
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
        [FromQuery] int start = 0,
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

    [HttpGet("random", Name = nameof(GetRandomSampleTrack))]
    [ProducesResponseType(typeof(List<TrackReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackReadDto>> GetRandomSampleTrack(
        [FromQuery][Range(1, 100)] int limit = 20,
        [FromQuery] TrackFilterSelectableRanged? filters = null)
    {
        return Ok(_mapper.Map<List<TrackReadDto>>(await _trackRepo.SampleRandomTrack(limit, filters)));
    }
}