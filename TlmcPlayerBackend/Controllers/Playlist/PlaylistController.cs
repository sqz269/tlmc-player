using AutoMapper;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.Playlist;
using TlmcPlayerBackend.Data.Api.UserProfile;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Controllers.Playlist;

public struct PlaylistInfo
{
    public string? Name { get; set; }
    public PlaylistVisibility? Visibility { get; set; }
}

[ApiController]
[Route("api/playlists")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PlaylistController : Controller
{
    private readonly IPlaylistRepo _playlistRepo;
    private readonly IUserProfileRepo _userProfileRepo;
    private readonly IMapper _mapper;


    public PlaylistController(
        IPlaylistRepo playlistRepo,
        IUserProfileRepo userProfileRepo,
        IMapper mapper)
    {
        _playlistRepo = playlistRepo;
        _userProfileRepo = userProfileRepo;
        _mapper = mapper;
    }

    /// <summary>
    /// Returns false when the caller has no user profile yet, in which case no
    /// playlists are created and the action should answer 400.
    /// </summary>
    private async Task<bool> CreatePersonalPlaylistIfNotExist(UserClaim userClaim)
    {
        if (await _playlistRepo.DoesPersonalPlaylistExist(userClaim.UserId))
        {
            return true;
        }

        // Playlist.OwnerId is a required FK to UserProfiles, so inserting the
        // personal playlists before the profile row exists fails inside the
        // database and surfaces as a 500. Every freshly registered identity hits
        // this on its first playlist request, so check up front and let the
        // caller return an actionable 400 instead.
        if (await _userProfileRepo.GetUserProfileById(userClaim.UserId) == null)
        {
            return false;
        }

        var history = Models.Playlist.Playlist.Create("History",
            PlaylistVisibility.Private, userClaim, PlaylistType.History);

        var queue = Models.Playlist.Playlist.Create("Queue",
            PlaylistVisibility.Private, userClaim, PlaylistType.Queue);

        var fav = Models.Playlist.Playlist.Create("Favorite",
            PlaylistVisibility.Private, userClaim, PlaylistType.Favorite);

        await _playlistRepo.InsertPlaylists(new List<Models.Playlist.Playlist> { history, queue, fav });
        return true;
    }

    private ActionResult ProfileRequired() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "User Profile Required",
            detail: "No user profile exists for this account. Create one with POST /api/user first.");

    [HttpGet("{playlistId:Guid}", Name = nameof(GetPlaylistById))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult<PlaylistReadDto>> GetPlaylistById(Guid playlistId)
    {
        var user = HttpContext.User.ToUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(playlistId, user?.UserId);

        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        return Ok(_mapper.Map<PlaylistReadDto>(playlist));
    }

    [HttpPost(Name = nameof(AddPlaylist))]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult<PlaylistReadDto>> AddPlaylist([FromBody] PlaylistInfo playlistInfo)
    {
        var user = HttpContext.User.ToUserClaim();

        if (string.IsNullOrWhiteSpace(playlistInfo.Name))
        {
            return BadRequest("Cannot create a playlist with an empty name");
        }

        playlistInfo.Visibility ??= PlaylistVisibility.Private;

        var playlist = Models.Playlist.Playlist.Create(playlistInfo.Name, playlistInfo.Visibility.Value, user);
        var inserted = await _playlistRepo.InsertPlaylist(playlist);

        if (inserted == null)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Playlist Not Created",
                detail: $"Playlist with Name: {playlistInfo.Name} Could not be created");
        }

        return CreatedAtRoute(nameof(GetPlaylistById), new { playlistId = inserted.Id },
            _mapper.Map<PlaylistReadDto>(inserted));
    }

    [HttpPut("{playlistId:Guid}", Name = nameof(UpdatePlaylistInfo))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult<PlaylistReadDto>> UpdatePlaylistInfo(Guid playlistId, [FromBody] PlaylistInfo playlistInfo)
    {
        var user = HttpContext.User.ToUserClaim();

        // Owner-scoped: a playlist the caller can merely *see* (public/unlisted)
        // must not be editable by them.
        var playlist = await _playlistRepo.GetOwnedPlaylist(playlistId, user.UserId);

        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        // Special playlists (History/Queue/Favorite) are fixed fixtures of an
        // account. Neither their name nor their visibility may be changed -- the
        // visibility half matters most, since these are created Private and hold
        // listening history the user never chose to publish.
        if (playlist.Type != PlaylistType.Normal)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Playlist Not Updated",
                detail: $"Playlist with Id: {playlistId} is a special playlist and cannot be modified");
        }

        if (playlistInfo.Name != null)
        {
            playlist.Name = playlistInfo.Name;
        }

        if (playlistInfo.Visibility != null)
        {
            playlist.Visibility = playlistInfo.Visibility.Value;
        }

        var saved = await _playlistRepo.SaveChanges();
        if (!saved)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Playlist Not Updated",
                detail: $"Playlist with Id: {playlistId} Could not be updated");
        }

        return Ok(_mapper.Map<PlaylistReadDto>(playlist));
    }

    [HttpDelete("{playlistId:Guid}", Name = nameof(DeletePlaylist))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeletePlaylist(Guid playlistId)
    {
        var user = HttpContext.User.ToUserClaim();

        var playlist = await _playlistRepo.GetOwnedPlaylist(playlistId, user.UserId);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        var deleted = await _playlistRepo.DeletePlaylist(playlistId, user.UserId);
        if (!deleted)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Playlist Not Deleted",
                detail: $"Playlist with Id: {playlistId} Could not be deleted");
        }

        return NoContent();
    }

    [HttpGet("user/{userId:Guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PlaylistReadDto>))]
    public async Task<ActionResult> GetUserPlaylists(Guid userId)
    {
        var user = HttpContext.User.ToUserClaim();

        var playlists = await _playlistRepo.GetUserPlaylist(userId, user?.UserId);
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(playlists));
    }

    [HttpGet("me", Name = nameof(GetCurrentUserPlaylists))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PlaylistReadDto>))]
    public async Task<ActionResult<PlaylistReadDto>> GetCurrentUserPlaylists()
    {
        var user = HttpContext.User.ToUserClaim();
        if (!await CreatePersonalPlaylistIfNotExist(user))
        {
            return ProfileRequired();
        }

        var playlists = await _playlistRepo.GetUserPlaylist(user.UserId, user.UserId);
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(playlists));
    }

    [HttpGet("me/history", Name = nameof(GetCurrentUserHistory))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserHistory()
    {
        var user = HttpContext.User.ToUserClaim();
        if (!await CreatePersonalPlaylistIfNotExist(user))
        {
            return ProfileRequired();
        }

        var history = await _playlistRepo.GetHistoryPlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(history));
    }

    [HttpGet("me/queue", Name = nameof(GetCurrentUserQueue))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserQueue()
    {
        var user = HttpContext.User.ToUserClaim();
        if (!await CreatePersonalPlaylistIfNotExist(user))
        {
            return ProfileRequired();
        }

        var queue = await _playlistRepo.GetQueuePlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(queue));
    }

    [HttpGet("me/favorite", Name = nameof(GetCurrentUserFavorite))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserFavorite()
    {
        var user = HttpContext.User.ToUserClaim();
        if (!await CreatePersonalPlaylistIfNotExist(user))
        {
            return ProfileRequired();
        }

        var fav = await _playlistRepo.GetFavoritesPlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(fav));
    }
}