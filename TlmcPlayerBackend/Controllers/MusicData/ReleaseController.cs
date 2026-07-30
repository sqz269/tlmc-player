using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/music/release")]
public class ReleaseController(IReleaseRepo releaseRepo) : ControllerBase
{
    private readonly IReleaseRepo _releaseRepo = releaseRepo;

    [HttpGet]
    public async Task<ActionResult<CursorPage<ReleaseListItemDto>>> GetReleases(
        [FromQuery] ReleaseSort sort = ReleaseSort.Name,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _releaseRepo.GetReleases(sort, cursor, limit);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReleaseReadDto>> GetRelease(ReleaseId id)
    {
        var release = await _releaseRepo.GetRelease(id);
        return release == null ? NotFound() : release;
    }
}
