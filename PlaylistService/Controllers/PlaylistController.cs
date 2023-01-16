using System.Collections.Specialized;
using System.Security.Claims;
using AuthServiceClientApi;
using AuthServiceClientApi.Utils;
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
    /// <param name="userId">The user that's sending the request, used for checking playlist visibility</param>
    /// <returns></returns>
    [HttpGet("{playlistId:Guid}", Name = nameof(GetPlaylist))]
    [RoleRequired(KnownRoles.User, KnownRoles.Guest)]
    [ProducesResponseType(typeof(PlaylistReadDto), StatusCodes.Status200OK)]
    public ActionResult<PlaylistReadDto> GetPlaylist(Guid playlistId)
    {
        var user = HttpContext.GetUserClaim();

        return Ok(_mapper.Map<PlaylistReadDto>(
            _playlistRepo.GetPlaylist(playlistId, user?.UserId)));
    }

    [HttpPost]
    [RoleRequired(KnownRoles.User)]
    public async Task<ActionResult<PlaylistReadDto>> CreatePlaylist([FromBody] PlaylistCreateRequest request)
    {
        var user = HttpContext.GetUserClaim();

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
    /// <param name="userId">The user that's sending the query</param>
    /// <returns></returns>
    [HttpGet("user/{userId:Guid}", Name = nameof(GetUserPlaylist))]
    [RoleRequired(KnownRoles.User, KnownRoles.Guest)]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetUserPlaylist(Guid userId)
    {
        var user = HttpContext.GetUserClaim();

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetUserPlaylist(userId, user?.UserId)));
    }

    [HttpGet("user/me", Name = nameof(GetCurrentUserPlaylist))]
    [RoleRequired(KnownRoles.User)]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserPlaylist()
    {
        // Not really possible for user to be null unless the AuthApiClient is implemented improperly
        var user = HttpContext.GetUserClaim();

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetUserPlaylist(user.UserId.Value, user.UserId.Value)));
    }

    [HttpGet("user/me/history", Name = nameof(GetCurrentUserHistory))]
    [RoleRequired(KnownRoles.User)]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserHistory()
    {
        var user = HttpContext.GetUserClaim();

        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetHistoryPlaylist(user.UserId.Value)));
    }

    [HttpGet("user/me/favorite", Name = nameof(GetCurrentUserFavorite))]
    [RoleRequired(KnownRoles.User)]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserFavorite()
    {
        var user = HttpContext.GetUserClaim();
         
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetFavoritesPlaylist(user.UserId.Value)));
    }

    [HttpGet("user/me/queue", Name = nameof(GetCurrentUserQueue))]
    [RoleRequired(KnownRoles.User)]
    [ProducesResponseType(typeof(IEnumerable<PlaylistReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetCurrentUserQueue()
    {
        var user = HttpContext.GetUserClaim();
           
        return Ok(_mapper.Map<IEnumerable<PlaylistReadDto>>(
            await _playlistRepo.GetQueuePlaylist(user.UserId.Value)));
    }
}