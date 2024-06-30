using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Dtos.MusicData.Album;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;









[ApiController]
[Route("api/music")]
public class AlbumController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly IMapper _mapper;

    private readonly long _totalAlbums;

    public AlbumController(
        IAlbumRepo albumRepo, 
        IMapper mapper)
    {
        _albumRepo = albumRepo;
        _mapper = mapper;
    }

    [HttpGet("album", Name = nameof(GetAlbums))]
    [ProducesResponseType(typeof(AlbumsListResult), StatusCodes.Status200OK)]
    //[RoleRequired(KnownRoles.Guest)]
    public async Task<AlbumsListResult> GetAlbums(
        [FromQuery] int start = 0, 
        [FromQuery] [Range(1, 50)] int limit = 20, 
        [FromQuery] AlbumOrderOptions sort = AlbumOrderOptions.Id, 
        [FromQuery] SortOrder sortOrder = SortOrder.Ascending)
    {
        var user = HttpContext.User;
        var claimIdentity = user.Identity as ClaimsIdentity;

        var (albums, total) = await _albumRepo.GetAlbums(start, limit, sort, sortOrder);
        var mapped = _mapper.Map<IEnumerable<Album>, IEnumerable<AlbumReadDto>>(albums);

        var albumReadDtos = mapped.ToList();

        return new AlbumsListResult
        {
            Albums = albumReadDtos,
            Count = albumReadDtos.Count,
            Total = total,
        };
    }

    [HttpPost("album/count", Name = nameof(CountAlbums))]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<long> CountAlbums()
    {
        return await _albumRepo.CountTotalAlbums();
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
    public async Task<ActionResult<IEnumerable<AlbumReadDto>>> GetAlbumFiltered(
        [FromQuery] AlbumFilter filter, 
        [FromQuery] int start = 0, 
        [FromQuery] [Range(1, 50)] int limit = 20)
    {
        return Ok(
            _mapper.Map<IEnumerable<AlbumReadDto>>(await _albumRepo.GetAlbumsFiltered(filter, start, limit)));
    }
}
