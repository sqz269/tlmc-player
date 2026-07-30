using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Controllers.Playlist;

[ApiController]
[Route("api/playlists")]
public class PlaylistController(IPlaylistRepo playlistRepo) : ControllerBase
{
    private readonly IPlaylistRepo _playlistRepo = playlistRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<PlaylistReadDto>>> GetMyPlaylists()
    {
        return await _playlistRepo.GetUserPlaylists(CurrentUser);
    }

    [HttpGet("favorite")]
    [Authorize]
    public async Task<ActionResult<PlaylistReadDto>> GetFavorite()
    {
        // Bootstrap race is settled by playlist_one_favorite_per_owner, not by luck.
        return await _playlistRepo.GetOrCreateFavorite(CurrentUser);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlaylistReadDto>> GetPlaylist(PlaylistId id)
    {
        var playlist = await _playlistRepo.GetPlaylist(id);
        if (playlist == null)
        {
            return NotFound();
        }

        return CanRead(playlist.OwnerId, playlist.Visibility) ? playlist : NotFound();
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PlaylistReadDto>> CreatePlaylist([FromBody] PlaylistWriteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
        {
            return BadRequest("Playlist name must be 1-200 characters");
        }

        return await _playlistRepo.CreatePlaylist(CurrentUser, dto.Name, dto.Visibility);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<PlaylistReadDto>> UpdatePlaylist(PlaylistId id, [FromBody] PlaylistWriteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
        {
            return BadRequest("Playlist name must be 1-200 characters");
        }

        var owned = await RequireOwned(id);
        if (owned != null)
        {
            return owned;
        }

        var updated = await _playlistRepo.UpdatePlaylist(id, dto.Name, dto.Visibility);
        return updated == null ? NotFound() : updated;
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeletePlaylist(PlaylistId id)
    {
        var meta = await _playlistRepo.GetPlaylistMeta(id);
        if (meta == null)
        {
            return NotFound();
        }

        if (meta.OwnerId != CurrentUser)
        {
            return NotFound();
        }

        if (meta.Kind == PlaylistKind.Favorite)
        {
            return BadRequest("The favorite playlist cannot be deleted");
        }

        await _playlistRepo.DeletePlaylist(id);
        return NoContent();
    }

    private bool CanRead(UserId ownerId, PlaylistVisibility visibility)
    {
        if (visibility is PlaylistVisibility.Public or PlaylistVisibility.Unlisted)
        {
            return true;
        }

        return User.Identity?.IsAuthenticated == true && ownerId == CurrentUser;
    }

    /// <summary>404 (not 403) for playlists that exist but are not yours: private
    /// playlist existence should not be observable.</summary>
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
