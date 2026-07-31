using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Auth;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.Playlist;

/// <summary>
/// Play history over append-only play_event rows. Replaces the History playlist
/// kind: history is a query, not a playlist to maintain.
/// </summary>
[ApiController]
[Route("api/user/history")]
// API keys are accepted alongside Keycloak (Docs/RECOMMENDER.md section 3a):
// the first-party client reports plays continuously with its device key, and
// phase 4 reads history with it for the territory overlay.
[Authorize(AuthenticationSchemes = $"Bearer,{ApiKeyAuthenticationHandler.SchemeName}")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class HistoryController(IPlayEventRepo playEventRepo) : ControllerBase
{
    private readonly IPlayEventRepo _playEventRepo = playEventRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpPost]
    public async Task<IActionResult> RecordPlay([FromBody] PlayEventWriteDto dto)
    {
        var recorded = await _playEventRepo.Record(CurrentUser, dto.TrackId, dto.Source, dto.MsPlayed);
        return recorded ? NoContent() : BadRequest("Unknown track");
    }

    [HttpGet]
    public async Task<ActionResult<CursorPage<PlayEventReadDto>>> GetHistory(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _playEventRepo.GetHistory(CurrentUser, cursor, limit);
    }
}
