using AuthServiceClientApi;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

[Route("api/music/album")]
[ApiController]
public class AlbumController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly IMapper _mapper;

    public AlbumController(IAlbumRepo albumRepo, IMapper mapper)
    {
        _albumRepo = albumRepo;
        _mapper = mapper;
    }

    [HttpGet("{id:Guid}", Name = nameof(GetAlbum))]
    public AlbumReadDto GetAlbum(Guid id)
    {
        var album = _albumRepo.GetAlbum(id);

        return _mapper.Map<Album, AlbumReadDto>(album);
    }

    [HttpPost("{id:Guid}/track")]
    public bool AddTrackToAlbum(Guid id, [FromBody] TrackWriteDto track)
    {
        _albumRepo.AddTrackToAlbum(id, _mapper.Map<TrackWriteDto, Track>(track));
        return true;
    }

    [HttpPost]
    public ActionResult<Album> AddAlbum([FromBody] AlbumWriteDto album)
    {
        var id = _albumRepo.AddAlbum(_mapper.Map<AlbumWriteDto, Album>(album));
        var result = _albumRepo.GetAlbum(id);

        //return _mapper.Map<Album, AlbumReadDto>(result);
        return CreatedAtRoute(nameof(GetAlbum), new {id = result.Id}, result);
    }
}