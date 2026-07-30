using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// Touhou reference data: original works and songs. Reads are public; the writes are
/// the thwiki pipeline's and sit behind the internal key despite the public prefix
/// (same arrangement 9fcc38c introduced).
/// </summary>
[ApiController]
[Route("api/source")]
public class OriginalController(IOriginalRepo originalRepo) : ControllerBase
{
    private readonly IOriginalRepo _originalRepo = originalRepo;

    [HttpGet("work")]
    public async Task<ActionResult<List<OriginalWorkReadDto>>> GetWorks()
    {
        return await _originalRepo.GetWorks();
    }

    [HttpGet("work/{id}")]
    public async Task<ActionResult<OriginalWorkReadDto>> GetWork(OriginalWorkId id)
    {
        var work = await _originalRepo.GetWork(id);
        return work == null ? NotFound() : work;
    }

    /// <summary>"Every arrangement of this original" — served by track_original_song's reverse index.</summary>
    [HttpGet("song/{id}/arrangements")]
    public async Task<ActionResult<CursorPage<TrackWithContext>>> GetArrangements(
        OriginalSongId id,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _originalRepo.GetArrangements(id, cursor, limit);
    }

    [HttpPost("work")]
    [InternalApiKey]
    public async Task<ActionResult<OriginalWorkReadDto>> UpsertWork([FromBody] OriginalWorkWriteDto dto)
    {
        return await _originalRepo.UpsertWork(dto);
    }

    [HttpPost("work/{workId}/song")]
    [InternalApiKey]
    public async Task<ActionResult<OriginalSongReadDto>> UpsertSong(
        OriginalWorkId workId, [FromBody] OriginalSongWriteDto dto)
    {
        var song = await _originalRepo.UpsertSong(workId, dto);
        return song == null ? NotFound($"Original work {workId} does not exist") : song;
    }
}
