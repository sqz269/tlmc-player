using KeycloakAuthProvider.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Auth;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// Personalized surfaces (Docs/RECOMMENDER.md section 4). Identity-only by
/// design — the anonymous surface has no taste to model — and reachable with
/// the per-device API key so the first-party client needs no live Keycloak
/// session. Serving a row logs its impressions; conversion is measured by the
/// rec_attribution view, never assumed.
/// </summary>
[ApiController]
[Route("api/music/recommendations")]
[Authorize(AuthenticationSchemes = $"Bearer,{ApiKeyAuthenticationHandler.SchemeName}")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class RecommendationsController(IRecommendationRepo recommendationRepo) : ControllerBase
{
    private readonly IRecommendationRepo _recommendationRepo = recommendationRepo;

    private UserId CurrentUser => new(User.ToUserClaim().UserId);

    [HttpGet("home")]
    public async Task<ActionResult<RecommendationHomeDto>> GetHome()
    {
        return await _recommendationRepo.GetHomeRows(CurrentUser);
    }

    /// <summary>
    /// Stateless session-adaptive radio (Docs/RECOMMENDER.md section 5): the
    /// client carries the session, so identical inputs replay identically and
    /// the server holds nothing between calls.
    /// </summary>
    [HttpGet("~/api/music/radio/next")]
    public async Task<ActionResult<RadioNextDto>> GetRadioNext(
        string? anchor,
        [FromQuery] string[]? completed,
        [FromQuery] string[]? skipped,
        [FromQuery] string[]? exclude,
        int count = 20)
    {
        TrackId? anchorId = TrackId.TryParse(anchor, null, out var parsed) ? parsed : null;
        var result = await _recommendationRepo.GetRadioNext(
            CurrentUser,
            anchorId,
            ParseIds(completed, 50),
            ParseIds(skipped, 50),
            ParseIds(exclude, 300),
            Math.Clamp(count, 1, 50));
        return result == null
            ? BadRequest("anchor or completed track ids are required")
            : result;
    }

    private static List<TrackId> ParseIds(string[]? raw, int cap)
        => (raw ?? [])
            .Select(x => TrackId.TryParse(x, null, out var id) ? (TrackId?)id : null)
            .OfType<TrackId>()
            .Take(cap)
            .ToList();
}
