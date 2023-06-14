using System.Collections.Specialized;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using PlaylistService.Data.Api;
using PlaylistService.Dtos;
using PlaylistService.Model;

namespace PlaylistService.Controllers;

public struct PlaylistCreateRequest
{
    public string Name { get; set; }
    public PlaylistVisibility Visibility { get; set; }
}

public class UserClaim
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; }

    public UserClaim(Guid? userId, string? username)
    {
        UserId = userId;
        Username = username;
    }
}


[ApiController]
[Route("api/playlist")]
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

    /// <summary>
    /// Retrieves a playlist
    /// </summary>
    /// <param name="playlistId">The playlist to get</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(PlaylistReadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlaylistReadDto>> GetPlaylist(Guid playlistId)
    {
        UserClaim user = null;

        return Ok(_mapper.Map<PlaylistReadDto>(
            await _playlistRepo.GetPlaylist(playlistId, user?.UserId)));
    }

    [HttpDelete("{playlistId:Guid}", Name = nameof(DeletePlaylist))]
    public async Task<ActionResult<bool>> DeletePlaylist(Guid playlistId)
    {
        UserClaim user = null;
        var playlist = await _playlistRepo.GetPlaylist(playlistId, true);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        if (playlist.UserId != user.UserId)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Insufficient Permission",
                detail: $"Insufficient Permission to delete playlist: {playlistId}");
        }

        return await _playlistRepo.DeletePlaylist(playlistId);
    }

    [HttpPost("", Name = nameof(CreatePlaylist))]
    public async Task<ActionResult<PlaylistReadDto>> CreatePlaylist([FromBody] PlaylistCreateRequest request)
    {
        UserClaim user = null;
        if (user == null)
        {
            return BadRequest();
        }

        var playlist = Playlist.Create(request.Name, request.Visibility, user);
        var inserted = await _playlistRepo.InsertPlaylist(playlist);
        return _mapper.Map<PlaylistReadDto>(inserted);
    }

    /// <summary>
    /// Gets all the playlist owned by a specific user
    /// </summary>
    /// <param name="userId">The user that owns the playlist</param>
    /// <returns></returns>
    [HttpGet("user/{userId:Guid}", Name = nameof(GetUserPlaylist))]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetUserPlaylist(Guid userId)
    {
        UserClaim user = null;
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetUserPlaylist(userId, user?.UserId)));
    }

    private async Task CreatePersonalPlaylistIfNotExist(UserClaim userClaim)
    {
        if (!await _playlistRepo.DoesPersonalPlaylistExist(userClaim.UserId.Value))
        {
            var history = Playlist.Create("History",
                PlaylistVisibility.Private, userClaim, PlaylistType.History);

            var queue = Playlist.Create("Queue",
                PlaylistVisibility.Private, userClaim, PlaylistType.Queue);

            var fav = Playlist.Create("Favorite",
                PlaylistVisibility.Private, userClaim, PlaylistType.Favorite);

            await _playlistRepo.InsertPlaylists(new List<Playlist> { history, queue, fav });
        }
    }

    [HttpGet("user/me", Name = nameof(GetCurrentUserPlaylist))]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserPlaylist()
    {
        // Not really possible for user to be null unless the AuthApiClient is implemented improperly
        UserClaim user = null;
        await CreatePersonalPlaylistIfNotExist(user);

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetUserPlaylist(user.UserId.Value, user.UserId.Value)));
    }

    [HttpGet("user/me/history", Name = nameof(GetCurrentUserHistory))]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserHistory()
    {
        UserClaim user = null;
        await CreatePersonalPlaylistIfNotExist(user);

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetHistoryPlaylist(user.UserId.Value)));
    }

    [HttpGet("user/me/favorite", Name = nameof(GetCurrentUserFavorite))]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserFavorite()
    {
        UserClaim user = null;
        await CreatePersonalPlaylistIfNotExist(user);

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetFavoritesPlaylist(user.UserId.Value)));
    }

    [HttpGet("user/me/queue", Name = nameof(GetCurrentUserQueue))]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserQueue()
    {
        UserClaim user = null;
        await CreatePersonalPlaylistIfNotExist(user);

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetQueuePlaylist(user.UserId.Value)));
    }
}