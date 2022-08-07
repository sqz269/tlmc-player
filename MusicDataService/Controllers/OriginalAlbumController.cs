using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

[ApiController]
[Route("api/source")]
public class OriginalAlbumController : Controller
{
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IOriginalAlbumRepo _originalAlbumRepo;
    private readonly IMapper _mapper;

    public OriginalAlbumController(IOriginalAlbumRepo originalAlbumRepo, IOriginalTrackRepo originalTrackRepo, IMapper mapper)
    {
        _originalAlbumRepo = originalAlbumRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;
    }

    [HttpGet("album")]
    [ProducesResponseType(typeof(IEnumerable<OriginalAlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<OriginalAlbumReadDto>> GetOriginalAlbums([FromQuery] int start = 0, [FromQuery] int limit = 20)
    {
        var albums = await _originalAlbumRepo.GetOriginalAlbums(start, limit);
        return _mapper.Map<IEnumerable<OriginalAlbum>, IEnumerable<OriginalAlbumReadDto>>(albums);
    }

    [HttpGet("album/{id}", Name = nameof(GetOriginalAlbum))]
    [ProducesResponseType(typeof(OriginalAlbumReadDto), StatusCodes.Status200OK)]
    public async Task<OriginalAlbumReadDto> GetOriginalAlbum(string id)
    {
        var album = await _originalAlbumRepo.GetOriginalAlbum(id);
        return _mapper.Map<OriginalAlbum, OriginalAlbumReadDto>(album);
    }

    [HttpPost("album")]
    [ProducesResponseType(typeof(ActionResult<OriginalAlbumReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<OriginalAlbumReadDto>> AddOriginalAlbum([FromBody] OriginalAlbumWriteDto album)
    {
        var id = await _originalAlbumRepo.AddOriginalAlbum(_mapper.Map<OriginalAlbumWriteDto, OriginalAlbum>(album));
        
        await _originalAlbumRepo.SaveChanges();

        var result = await _originalAlbumRepo.GetOriginalAlbum(id);

        return CreatedAtRoute(nameof(GetOriginalAlbum), new { id = result.Id }, 
            _mapper.Map<OriginalAlbum, OriginalAlbumReadDto>(result));
    }

    [HttpPost("album/{albumId}/track")]
    [ProducesResponseType(typeof(ActionResult<OriginalTrackReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<OriginalTrackReadDto>> AddOriginTrack(string albumId, [FromBody] OriginalTrackWriteDto track)
    {
        var addedTrack = await _originalAlbumRepo.AddOriginalTrackToAlbum(albumId, _mapper.Map<OriginalTrackWriteDto, OriginalTrack>(track));

        await _originalAlbumRepo.SaveChanges();

        return CreatedAtRoute(nameof(GetOriginalTrack), new { id = addedTrack.Id },
            _mapper.Map<OriginalTrack, OriginalTrackReadDto>(addedTrack));
    }

    [HttpGet("track")]
    [ProducesResponseType(typeof(IEnumerable<OriginalTrackReadDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<OriginalTrackReadDto>> GetOriginalTracks([FromQuery] int start = 0, [FromQuery] int limit = 20)
    {
        var tracks = await _originalTrackRepo.GetOriginalTracks(start, limit);
        return _mapper.Map<IEnumerable<OriginalTrack>, IEnumerable<OriginalTrackReadDto>>(tracks);
    }

    [HttpGet("track/{id}", Name = nameof(GetOriginalTrack))]
    [ProducesResponseType(typeof(OriginalTrackReadDto), StatusCodes.Status200OK)]
    public async Task<OriginalTrackReadDto?> GetOriginalTrack(string id)
    {
        var track = await _originalTrackRepo.GetOriginalTrack(id);
        if (track == null)
            return null;
        return _mapper.Map<OriginalTrack, OriginalTrackReadDto>(track);
    }
}