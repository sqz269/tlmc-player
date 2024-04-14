using AutoMapper;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.Playlist;
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
    private readonly IMapper _mapper;


    public PlaylistController(
        IPlaylistRepo playlistRepo,
        IMapper mapper)
    {
        _playlistRepo = playlistRepo;
        _mapper = mapper;
    }

    private async Task CreatePersonalPlaylistIfNotExist(UserClaim userClaim)
    {
        if (!await _playlistRepo.DoesPersonalPlaylistExist(userClaim.UserId))
        {
            var history = Models.Playlist.Playlist.Create("History",
                PlaylistVisibility.Private, userClaim, PlaylistType.History);

            var queue = Models.Playlist.Playlist.Create("Queue",
                PlaylistVisibility.Private, userClaim, PlaylistType.Queue);

            var fav = Models.Playlist.Playlist.Create("Favorite",
                PlaylistVisibility.Private, userClaim, PlaylistType.Favorite);

            await _playlistRepo.InsertPlaylists(new List<Models.Playlist.Playlist> { history, queue, fav });
        }
    }

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

        var playlist = await _playlistRepo.GetPlaylist(playlistId, user?.UserId);

        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        // Make sure playlist isn't special playlist
        if (playlist?.Type != PlaylistType.Normal && playlistInfo.Name != null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Playlist Not Updated",
                detail: $"Playlist with Id: {playlistId} is a special playlist and it's name cannot be updated");
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

        var playlist = await _playlistRepo.GetPlaylist(playlistId, user?.UserId);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        var deleted = await _playlistRepo.DeletePlaylist(playlistId);
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
        await CreatePersonalPlaylistIfNotExist(user);

        var playlists = await _playlistRepo.GetUserPlaylist(user.UserId, user.UserId);
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(playlists));
    }

    [HttpGet("me/history", Name = nameof(GetCurrentUserHistory))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserHistory()
    {
        var user = HttpContext.User.ToUserClaim();
        await CreatePersonalPlaylistIfNotExist(user);

        var history = await _playlistRepo.GetHistoryPlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(history));
    }

    [HttpGet("me/queue", Name = nameof(GetCurrentUserQueue))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserQueue()
    {
        var user = HttpContext.User.ToUserClaim();
        await CreatePersonalPlaylistIfNotExist(user);

        var queue = await _playlistRepo.GetQueuePlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(queue));
    }

    [HttpGet("me/favorite", Name = nameof(GetCurrentUserFavorite))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistReadDto))]
    public async Task<ActionResult> GetCurrentUserFavorite()
    {
        var user = HttpContext.User.ToUserClaim();
        await CreatePersonalPlaylistIfNotExist(user);

        var fav = await _playlistRepo.GetFavoritesPlaylist(user.UserId);
        return Ok(_mapper.Map<PlaylistReadDto>(fav));
    }
}