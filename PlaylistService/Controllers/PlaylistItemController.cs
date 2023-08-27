using System.ComponentModel.DataAnnotations;
using AutoMapper;
using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaylistService.Data.Api;
using PlaylistService.Dtos;
using PlaylistService.Model;

namespace PlaylistService.Controllers;

[ApiController]
[Route("api/playlists/{playlistId:guid}/tracks")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PlaylistItemsController : Controller
{
    private readonly IPlaylistRepo _playlistRepo;
    private readonly IPlaylistItemRepo _playlistItemRepo;
    private readonly IMapper _mapper;

    public PlaylistItemsController(
        IPlaylistRepo playlistRepo,
        IPlaylistItemRepo playlistItemRepo,
        IMapper mapper)
    {
        _playlistRepo = playlistRepo;
        _playlistItemRepo = playlistItemRepo;
        _mapper = mapper;
    }

    [HttpGet("", Name = nameof(GetPlaylistItems))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<PlaylistItemReadDto>))]
    public async Task<ActionResult<List<PlaylistItemReadDto>>> GetPlaylistItems(Guid playlistId,
        [FromQuery] int start = 0, [FromQuery][Range(1, 50)] int limit = 20)
    {
        var userClaim = HttpContext.User.ToUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(playlistId, userClaim.UserId);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        var playlistItems = await _playlistItemRepo.GetPlaylistItems(playlistId, start, limit);

        return Ok(_mapper.Map<List<PlaylistItem>, List<PlaylistItemReadDto>>(playlistItems));
    }

    [HttpPost("", Name = nameof(AddTrackToPlaylist))]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(List<PlaylistItemReadDto>))]
    public async Task<ActionResult> AddTrackToPlaylist(Guid playlistId, [FromBody] List<Guid> trackIds)
    {
        var userClaim = HttpContext.User.ToUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(playlistId, userClaim.UserId);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        var addedItems = await _playlistItemRepo.InsertPlaylistItems(playlistId, trackIds);

        return CreatedAtRoute(nameof(GetPlaylistItems), new { playlistId }, addedItems);
    }

    [HttpDelete("", Name = nameof(DeleteTrackFromPlaylist))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteTrackFromPlaylist(Guid playlistId, [FromBody] List<Guid> trackIds)
    {
        var userClaim = HttpContext.User.ToUserClaim();

        var playlist = await _playlistRepo.GetPlaylist(playlistId, userClaim.UserId);
        if (playlist == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Playlist Not Found",
                detail: $"Playlist with Id: {playlistId} Does not exist");
        }

        var deletedItems = await _playlistItemRepo.DeletePlaylistItems(playlistId, trackIds);

        return NoContent();
    }
}