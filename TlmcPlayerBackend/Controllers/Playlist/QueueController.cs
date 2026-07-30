using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.Playlist;

/// <summary>
/// The play queue — not a playlist (SCHEMA-V6.md section 8): one per user, never
/// shared, duplicates welcome. Entries are addressed by queue item id, because the
/// same track can legitimately appear twice.
/// </summary>
[ApiController]
[Route("api/queue")]
[Authorize]
public class QueueController(IQueueRepo queueRepo) : ControllerBase
{
    private readonly IQueueRepo _queueRepo = queueRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet]
    public async Task<ActionResult<List<QueueItemDto>>> GetQueue()
    {
        return await _queueRepo.GetQueue(CurrentUser);
    }

    [HttpPut]
    public async Task<IActionResult> ReplaceQueue([FromBody] QueueWriteDto dto)
    {
        var unknown = await _queueRepo.Replace(CurrentUser, dto.TrackIds);
        return unknown.Count > 0
            ? BadRequest(new { unknown_track_ids = unknown })
            : NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Enqueue([FromBody] QueueEnqueueDto dto)
    {
        if (dto.TrackIds.Count == 0)
        {
            return BadRequest("track_ids must not be empty");
        }

        var unknown = await _queueRepo.Enqueue(CurrentUser, dto.TrackIds, dto.AtFront);
        return unknown.Count > 0
            ? BadRequest(new { unknown_track_ids = unknown })
            : NoContent();
    }

    [HttpDelete("{itemId:long}")]
    public async Task<IActionResult> RemoveItem(long itemId)
    {
        var removed = await _queueRepo.RemoveItem(CurrentUser, itemId);
        return removed ? NoContent() : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _queueRepo.Clear(CurrentUser);
        return NoContent();
    }
}
