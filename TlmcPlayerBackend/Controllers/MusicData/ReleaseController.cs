using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/music/release")]
public class ReleaseController(
    IReleaseRepo releaseRepo, ISimilarityRepo similarityRepo) : ControllerBase
{
    private readonly IReleaseRepo _releaseRepo = releaseRepo;
    private readonly ISimilarityRepo _similarityRepo = similarityRepo;

    [HttpGet]
    public async Task<ActionResult<CursorPage<ReleaseListItemDto>>> GetReleases(
        [FromQuery] ReleaseSort sort = ReleaseSort.Name,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _releaseRepo.GetReleases(sort, cursor, limit);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReleaseReadDto>> GetRelease(ReleaseId id)
    {
        var release = await _releaseRepo.GetRelease(id);
        return release == null ? NotFound() : release;
    }

    [HttpGet("{id}/similar")]
    public async Task<ActionResult<SimilarReleasesResponseDto>> GetSimilar(
        ReleaseId id,
        [FromQuery] string flavor = "style",
        [FromQuery] int limit = 20)
    {
        if (!SimilarityFlavor.TryParse(flavor, out var parsed))
        {
            return BadRequest("flavor must be 'style' or 'kde'");
        }

        limit = Math.Clamp(limit, 1, 100);
        var rows = await _similarityRepo.GetSimilarReleases(id, parsed, limit);
        if (rows.Count == 0 && await _releaseRepo.GetRelease(id) == null)
        {
            return NotFound();
        }

        return new SimilarReleasesResponseDto
        {
            Items = ToItems(rows, parsed == GroupSimilarityFlavor.Kde
                ? r => r.ScoreKde
                : r => r.ScoreStyle),
            Flavor = parsed == GroupSimilarityFlavor.Kde ? "kde" : "style",
            Model = await _similarityRepo.GetModel(),
        };
    }

    /// <summary>
    /// Releases sharing recordings with this one — re-releases, compilations,
    /// duplicate rips. Ordered by raw chamfer, floored at minScore.
    /// </summary>
    [HttpGet("{id}/other-releases")]
    public async Task<ActionResult<SimilarReleasesResponseDto>> GetOtherReleases(
        ReleaseId id,
        [FromQuery] float minScore = 0.999f,
        [FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 20);
        var rows = await _similarityRepo.GetOtherReleases(id, minScore, limit);
        if (rows.Count == 0 && await _releaseRepo.GetRelease(id) == null)
        {
            return NotFound();
        }

        return new SimilarReleasesResponseDto
        {
            Items = ToItems(rows, r => r.ScoreRaw),
            Flavor = "raw",
            Model = await _similarityRepo.GetModel(),
        };
    }

    private static List<SimilarReleaseItemDto> ToItems(
        List<SimilarReleaseCandidate> rows,
        Func<SimilarReleaseCandidate, float> orderScore)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var min = rows.Min(orderScore);
        var max = rows.Max(orderScore);
        var span = max - min;
        return rows
            .Select(r => new SimilarReleaseItemDto
            {
                Release = r.Release,
                ArtworkId = r.ArtworkId,
                Circles = r.Circles,
                ScoreStyle = r.ScoreStyle,
                ScoreRaw = r.ScoreRaw,
                ScoreKde = r.ScoreKde,
                Relevance = span <= 0 ? 1f : (orderScore(r) - min) / span,
            })
            .ToList();
    }
}
