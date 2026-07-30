using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/entity/circle")]
public class CircleController(ICircleRepo circleRepo, IReleaseRepo releaseRepo) : ControllerBase
{
    private readonly ICircleRepo _circleRepo = circleRepo;
    private readonly IReleaseRepo _releaseRepo = releaseRepo;

    [HttpGet]
    public async Task<ActionResult<CursorPage<CircleReadDto>>> GetCircles(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _circleRepo.GetCircles(cursor, limit);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CircleReadDto>> GetCircle(CircleId id)
    {
        var circle = await _circleRepo.GetCircle(id);
        return circle == null ? NotFound() : circle;
    }

    /// <summary>Exact name or alias lookup; used by the ETL to map scraped names.</summary>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<CircleReadDto>> GetCircleByName(string name)
    {
        var circle = await _circleRepo.GetCircleByName(name);
        return circle == null ? NotFound() : circle;
    }

    /// <summary>"Releases by this circle" — the browse path AlbumCircle never indexed.</summary>
    [HttpGet("{id}/releases")]
    public async Task<ActionResult<CursorPage<ReleaseListItemDto>>> GetReleases(
        CircleId id,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _releaseRepo.GetReleasesByCircle(id, cursor, limit);
    }
}
