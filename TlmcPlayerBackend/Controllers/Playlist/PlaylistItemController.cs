using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Controllers.Playlist;

[ApiController]
[Route("api/playlists/{playlistId}/items")]
public class PlaylistItemController(IPlaylistRepo playlistRepo) : ControllerBase
{
    private readonly IPlaylistRepo _playlistRepo = playlistRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet]
    public async Task<ActionResult<List<PlaylistItemDto>>> GetItems(
        PlaylistId playlistId,
        [FromQuery] int start = 0,
        [FromQuery] int limit = 100)
    {
        var meta = await _playlistRepo.GetPlaylistMeta(playlistId);
        if (meta == null || !CanRead(meta))
        {
            return NotFound();
        }

        limit = Math.Clamp(limit, 1, 500);
        return await _playlistRepo.GetItems(playlistId, Math.Max(0, start), limit);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddItems(PlaylistId playlistId, [FromBody] PlaylistItemsWriteDto dto)
    {
        if (dto.TrackIds.Count == 0)
        {
            return BadRequest("track_ids must not be empty");
        }

        var denied = await RequireOwned(playlistId);
        if (denied != null)
        {
            return denied;
        }

        var unknown = await _playlistRepo.AddItems(playlistId, dto.TrackIds);
        return unknown.Count > 0
            ? BadRequest(new { unknown_track_ids = unknown })
            : NoContent();
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> RemoveItems(PlaylistId playlistId, [FromBody] PlaylistItemsWriteDto dto)
    {
        if (dto.TrackIds.Count == 0)
        {
            return BadRequest("track_ids must not be empty");
        }

        var denied = await RequireOwned(playlistId);
        if (denied != null)
        {
            return denied;
        }

        await _playlistRepo.RemoveItems(playlistId, dto.TrackIds);
        return NoContent();
    }

    [HttpPut("move")]
    [Authorize]
    public async Task<IActionResult> MoveItem(PlaylistId playlistId, [FromBody] PlaylistItemMoveDto dto)
    {
        var denied = await RequireOwned(playlistId);
        if (denied != null)
        {
            return denied;
        }

        var moved = await _playlistRepo.MoveItem(playlistId, dto.TrackId, dto.ToPosition);
        return moved ? NoContent() : NotFound();
    }

    private bool CanRead(PlaylistMeta meta)
    {
        if (meta.Visibility is PlaylistVisibility.Public or PlaylistVisibility.Unlisted)
        {
            return true;
        }

        return User.Identity?.IsAuthenticated == true && meta.OwnerId == CurrentUser;
    }

    private async Task<ActionResult?> RequireOwned(PlaylistId id)
    {
        var meta = await _playlistRepo.GetPlaylistMeta(id);
        if (meta == null || meta.OwnerId != CurrentUser)
        {
            return NotFound();
        }

        return null;
    }
}
