using AuthServiceClientApi;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

[Route("api/music")]
[ApiController]
public class AlbumController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IMapper _mapper;

    public AlbumController(IAlbumRepo albumRepo, ITrackRepo trackRepo, IOriginalTrackRepo originalTrackRepo, IMapper mapper)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;
    }

    [HttpGet("album")]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<AlbumReadDto>> GetAlbums([FromQuery] int start = 0, [FromQuery] int limit = 20)
    {
        var albums = await _albumRepo.GetAlbums(start, limit);
        return _mapper.Map<IEnumerable<Album>, IEnumerable<AlbumReadDto>>(albums);
    }

    [HttpGet("album/{id:Guid}", Name = nameof(GetAlbum))]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(AlbumWriteDto), StatusCodes.Status200OK)]
    public async Task<AlbumReadDto> GetAlbum(Guid id)
    {
        var album = await _albumRepo.GetAlbum(id);
        if (album == null)
            NotFound();
        return _mapper.Map<Album, AlbumReadDto>(album);
    }

    [HttpPost("album")]
    [ProducesResponseType(typeof(ActionResult<AlbumReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<AlbumReadDto>> AddAlbum([FromBody] AlbumWriteDto album)
    {
        var id = await _albumRepo.AddAlbum(_mapper.Map<AlbumWriteDto, Album>(album));

        await _albumRepo.SaveChanges();

        var result = await _albumRepo.GetAlbum(id);

        //return _mapper.Map<Album, AlbumReadDto>(result);
        return CreatedAtRoute(nameof(GetAlbum), new {id = result.Id}, _mapper.Map<Album, AlbumReadDto>(result));
    }

    [HttpPost("album/{albumId:Guid}/track")]
    [ProducesResponseType(typeof(ActionResult<TrackReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrackReadDto>> AddTrack(Guid albumId, [FromBody] TrackWriteDto track)
    {
        var trackModel = _mapper.Map<TrackWriteDto, Track>(track);

        // We ignored Track.Original mapping when converting from Dto to Model
        // we need to resolve it manually
        if (track.Original == null)
        {
            trackModel.Original = new List<OriginalTrack>();
        }
        else
        {
            var originalTracks = (await _originalTrackRepo.GetOriginalTracks(track.Original)).ToList();
            if (originalTracks.Count != track.Original.Count)
            {
                throw new ArgumentException("One of the OriginalTracks is invalid or does not exist.");
            }
            trackModel.Original = originalTracks;
        }

        var addedTrack = await _albumRepo.AddTrackToAlbum(albumId, trackModel);

        await _albumRepo.SaveChanges();

        return CreatedAtRoute(nameof(GetTrack), new { id = addedTrack.Id },
            _mapper.Map<Track, TrackReadDto>(addedTrack));
    }

    [HttpGet("track/{id:Guid}", Name = nameof(GetTrack))]
    [ProducesResponseType(typeof(TrackReadDto), StatusCodes.Status200OK)]
    public async Task<TrackReadDto?> GetTrack(Guid id)
    {
        var track = await _trackRepo.GetTrack(id);
        if (track == null)
            return null;
        return _mapper.Map<Track, TrackReadDto>(track);
    }
}