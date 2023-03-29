using System.Collections;
using AuthServiceClientApi;
using AutoMapper;
using AutoMapper.QueryableExtensions.Impl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Dtos.Album;
using MusicDataService.Dtos.Track;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

public class AlbumFilter
{
    public string? Title { get; set; }
    public DateTime? ReleaseDateBegin { get; set; }
    public DateTime? ReleaseDateEnd { get; set; }
    public string? Convention { get; set; }
    public string? Catalog { get; set; }
    public string? Artist { get; set; }
    public Guid? ArtistId { get; set; }
}

public class TrackFilter
{
    public string? Title { get; set; }
    public List<string>? Original { get; set; } = new();
    public List<string>? OriginalId { get; set; } = new();
    public List<string>? Staff { get; set; } = new();
}

public class TrackGetMultipleResp
{
    public IEnumerable<TrackReadDto> Tracks { get; set; }
    public IEnumerable<Guid> NotFound { get; set; }
}

[ApiController]
[Route("api/music")]
public class AlbumController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IMapper _mapper;

    private readonly LinkGenerator _linkGenerator;
    private readonly Func<Guid, string?> _assetLinkGenerator;

    private readonly long _totalAlbums;

    public AlbumController(
        IAlbumRepo albumRepo, 
        ITrackRepo trackRepo, 
        IOriginalTrackRepo originalTrackRepo, 
        IMapper mapper,
        LinkGenerator linkGenerator)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;

        _linkGenerator = linkGenerator;
        _assetLinkGenerator = assetId =>
            _linkGenerator.GetUriByName(HttpContext,
                nameof(AssetController.GetAsset),
                new { Id = assetId },
                fragment: FragmentString.Empty);
    }

    [HttpGet("album", Name = nameof(GetAlbums))]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    //[RoleRequired(KnownRoles.Guest)]
    public async Task<IEnumerable<AlbumReadDto>> GetAlbums([FromQuery] int start = 0, [FromQuery] int limit = 20)
    {
        var albums = await _albumRepo.GetAlbums(start, limit);
        var mapped = _mapper.Map<IEnumerable<Album>, IEnumerable<AlbumReadDto>>(albums);

        var albumReadDtos = mapped.ToList();

        return albumReadDtos;
    }

    [HttpPost("album", Name = nameof(GetAlbumsByIds))]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<AlbumReadDto>> GetAlbumsByIds([FromBody] IEnumerable<Guid> albumIds)
    {
        var idLists = albumIds.ToList();

        var (found, notFound) = await _albumRepo.GetAlbums(idLists);

        var albumReadDtos = _mapper.Map<IEnumerable<Album>, IEnumerable<AlbumReadDto>>(found);

        return albumReadDtos;
    }

    [HttpGet("album/{id:Guid}", Name = nameof(GetAlbum))]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(AlbumReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<Album>> GetAlbum(Guid id)
    {
        var album = await _albumRepo.GetAlbum(id);
        if (album == null)
            return NotFound();
        var mapped = _mapper.Map<Album, AlbumReadDto>(album);
        return Ok(mapped);
    }

    [HttpGet("album/filter", Name = nameof(GetAlbumFiltered))]
    public async Task<ActionResult<IEnumerable<AlbumReadDto>>> GetAlbumFiltered([FromQuery] AlbumFilter filter, [FromQuery] int start = 0, [FromQuery] int limit = 20)
    {
        return Ok(
            _mapper.Map<IEnumerable<AlbumReadDto>>(await _albumRepo.GetAlbumsFiltered(filter, start, limit)));
    }

    [DevelopmentOnly]
    [HttpPost("album/create", Name = nameof(AddAlbum))]
    [ProducesResponseType(typeof(ActionResult<AlbumReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<AlbumReadDto>> AddAlbum([FromBody] AlbumWriteDto album)
    {
        var id = await _albumRepo.AddAlbum(_mapper.Map<AlbumWriteDto, Album>(album));

        await _albumRepo.SaveChanges();

        var result = await _albumRepo.GetAlbum(id);

        //return _mapper.Map<Album, AlbumReadDto>(result);
        return CreatedAtRoute(nameof(GetAlbum), new {id = result.Id}, _mapper.Map<Album, AlbumReadDto>(result));
    }

    [DevelopmentOnly]
    [HttpPost("album/{albumId:Guid}/track/create", Name = nameof(AddTrack))]
    [ProducesResponseType(typeof(ActionResult<TrackReadDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrackReadDto>> AddTrack(Guid albumId, [FromBody] TrackWriteDto track)
    {
        var trackModel = _mapper.Map<TrackWriteDto, Track>(track);

        // We ignored Track.Original mapping when converting from Dto to Model
        // we need to resolve it manually

        var originalTracks = (await _originalTrackRepo.GetOriginalTracks(track.Original)).ToList();
        if (originalTracks.Count != track.Original.Count)
        {
            throw new ArgumentException("One of the OriginalTracks is invalid or does not exist.");
        }
        trackModel.Original.AddRange(originalTracks);

        var addedTrack = await _albumRepo.AddTrackToAlbum(albumId, trackModel);

        await _albumRepo.SaveChanges();

        return CreatedAtRoute(nameof(GetTrack), new { id = addedTrack.Id },
            _mapper.Map<Track, TrackReadDto>(addedTrack));
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
}
