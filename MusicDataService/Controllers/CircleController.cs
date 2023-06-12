using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data.Api;
using MusicDataService.Dtos.Album;
using MusicDataService.Dtos.Circle;

namespace MusicDataService.Controllers;

[ApiController]
[Route("api/entity/circle")]
public class CircleController : Controller
{
    private readonly ICircleRepo _circleRepo;
    private readonly IMapper _mapper;

    public CircleController(ICircleRepo circleRepo, IMapper mapper)
    {
        _circleRepo = circleRepo;
        _mapper = mapper;
    }

    [HttpGet("", Name = nameof(GetCircles))]
    // TODO: Return 400 when limit exceeds certain number
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<CircleReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CircleReadDto>>> GetCircles([FromQuery] int start = 0, [FromQuery] [Range(1, 50)] int limit = 20)
    {
        return Ok(_mapper.Map<IEnumerable<CircleReadDto>>(await _circleRepo.GetCircles(start, limit)));
    }

    [HttpGet("{id:Guid}", Name = nameof(GetCircleById))]
    [ProducesResponseType(typeof(CircleReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CircleReadDto>> GetCircleById(Guid id)
    {
        var circle = await _circleRepo.GetCircleById(id);
        if (circle == null)
        {
            return NotFound();
        }

        return Ok(_mapper.Map<CircleReadDto>(circle));
    }

    [HttpGet("{name}", Name = nameof(GetCircleByName))]
    [ProducesResponseType(typeof(CircleReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CircleReadDto>> GetCircleByName(string name)
    {
        var circle = await _circleRepo.GetCircleByName(name);
        if (circle == null)
        {
            return NotFound();
        }

        return Ok(_mapper.Map<CircleReadDto>(circle));
    }

    [HttpGet("{name}/albums", Name = nameof(GetCircleAlbumsByName))]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AlbumReadDto>>> GetCircleAlbumsByName(string name, [FromQuery] int start = 0, [FromQuery] [Range(1, 50)] int limit = 20)
    {
        return Ok(_mapper.Map<IEnumerable<AlbumReadDto>>(
            await _circleRepo.GetCircleAlbums(name, start, limit)
        ));
    }

    [HttpGet("{id:Guid}/albums", Name = nameof(GetCircleAlbumsById))]
    [ProducesResponseType(typeof(IEnumerable<AlbumReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AlbumReadDto>>> GetCircleAlbumsById(Guid id, [FromQuery] int start = 0, [FromQuery] [Range(1, 50)] int limit = 20)
    {
        return Ok(_mapper.Map<IEnumerable<AlbumReadDto>>(
            await _circleRepo.GetCircleAlbums(id, start, limit)
        ));
    }
}