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
}
