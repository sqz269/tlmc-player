using AuthServiceClientApi;
using AuthServiceClientApi.Utils;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using PlaylistService.Data.Api;
using PlaylistService.Data.Impl;
using PlaylistService.Dtos;
using PlaylistService.Model;

namespace PlaylistService.Controllers;

public struct PlaylistItemAddRequest
{
    public Guid PlaylistId { get; set; }
    public Guid PlaylistItemId { get; set; }
    // public int? Position { get; set; }
}

public struct PlaylistItemDeleteRequest
{
    public Guid PlaylistId { get; set; }
    public Guid PlaylistItemId { get; set; }
}

public struct PlaylistItemGetRequest
{
    public Guid PlaylistId { get; set; }
    public Guid PlaylistItemId { get; set; }
}

[ApiController]
[Route("api/playlistItem")]
public class PlaylistItemController : Controller
{
    private readonly IPlaylistRepo _playlistRepo;
    private readonly IPlaylistItemRepo _playlistItemRepo;
    private readonly IMapper _mapper;

    public PlaylistItemController(IPlaylistRepo playlistRepo,
        IPlaylistItemRepo playlistItemRepo,
        IMapper mapper)
    {
        _playlistRepo = playlistRepo;
        _playlistItemRepo = playlistItemRepo;
        _mapper = mapper;
    }

    [HttpPost("", Name = nameof(AddPlaylistItemToPlaylist))]
    [RoleRequired(KnownRoles.User)]
    [ProducesResponseType(typeof(PlaylistItemReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] // Invalid Position
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] // User doesn't have right to add
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // Playlist doesn't exist (or track doesn't exit [if it will be ever implemented)
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] // Track already exists in the playlist
    public async Task<ActionResult<PlaylistItemReadDto>> AddPlaylistItemToPlaylist([FromBody] PlaylistItemAddRequest request)
    {
        // TODO: Communicate with MusicDataService and verify if the track id is actually valid
        var user = HttpContext.GetUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(request.PlaylistId, true);

        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {request.PlaylistId} Does not exist");
        }

        if (playlist.UserId != user?.UserId)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Access Denied",
                detail: "User Does not have write access to this playlist");
        }

        if (playlist.Tracks.Any(t => t.PlaylistId == request.PlaylistItemId))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Track Already Exists",
                detail: $"Track with Id: {request.PlaylistItemId} Already exists in playlist");
        }

        var added = await _playlistItemRepo.InsertPlaylistItem(request.PlaylistId, request.PlaylistItemId);
        return Ok(_mapper.Map<PlaylistItem, PlaylistItemReadDto>(added));
    }

    [HttpDelete("", Name = nameof(DeletePlaylistItemFromPlaylist))]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] // User doesn't have right to remove
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // Playlist doesn't exist or track that we are removing doesn't exist in playlist
    public async Task<ActionResult> DeletePlaylistItemFromPlaylist([FromBody] PlaylistItemDeleteRequest request)
    {
        var user = HttpContext.GetUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(request.PlaylistId, true);

        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {request.PlaylistId} Does not exist");
        }

        if (playlist.UserId != user?.UserId)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Access Denied",
                detail: "User Does not have write access to this playlist");
        }

        if (!playlist.Tracks.Any(t => t.PlaylistId == request.PlaylistItemId))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Track Does Not Exist",
                detail: $"Track with Id: {request.PlaylistItemId} Does not exists in playlist");
        }
    }
}