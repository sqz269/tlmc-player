using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/entity/circle")]
public class CircleController(
    ICircleRepo circleRepo, IReleaseRepo releaseRepo,
    ISimilarityRepo similarityRepo) : ControllerBase
{
    private readonly ICircleRepo _circleRepo = circleRepo;
    private readonly IReleaseRepo _releaseRepo = releaseRepo;
    private readonly ISimilarityRepo _similarityRepo = similarityRepo;

    [HttpGet]
    public async Task<ActionResult<CursorPage<CircleReadDto>>> GetCircles(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _circleRepo.GetCircles(cursor, limit);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CircleReadDto>> GetCircle(CircleId id)
    {
        var circle = await _circleRepo.GetCircle(id);
        return circle == null ? NotFound() : circle;
    }

    /// <summary>Exact name or alias lookup; used by the ETL to map scraped names.</summary>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<CircleReadDto>> GetCircleByName(string name)
    {
        var circle = await _circleRepo.GetCircleByName(name);
        return circle == null ? NotFound() : circle;
    }

    /// <summary>"Releases by this circle" — the browse path AlbumCircle never indexed.</summary>
    [HttpGet("{id}/releases")]
    public async Task<ActionResult<CursorPage<ReleaseListItemDto>>> GetReleases(
        CircleId id,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        return await _releaseRepo.GetReleasesByCircle(id, cursor, limit);
    }

    [HttpGet("{id}/similar")]
    public async Task<ActionResult<SimilarCirclesResponseDto>> GetSimilar(
        CircleId id,
        [FromQuery] string flavor = "style",
        [FromQuery] int limit = 20)
    {
        if (!SimilarityFlavor.TryParse(flavor, out var parsed))
        {
            return BadRequest("flavor must be 'style' or 'kde'");
        }

        limit = Math.Clamp(limit, 1, 100);
        var rows = await _similarityRepo.GetSimilarCircles(id, parsed, limit);
        if (rows.Count == 0 && await _circleRepo.GetCircle(id) == null)
        {
            return NotFound();
        }

        Func<SimilarCircleCandidate, float> orderScore =
            parsed == GroupSimilarityFlavor.Kde ? r => r.ScoreKde : r => r.ScoreStyle;
        var min = rows.Count == 0 ? 0f : rows.Min(orderScore);
        var max = rows.Count == 0 ? 0f : rows.Max(orderScore);
        var span = max - min;

        return new SimilarCirclesResponseDto
        {
            Items = rows
                .Select(r => new SimilarCircleItemDto
                {
                    Circle = r.Circle,
                    ScoreStyle = r.ScoreStyle,
                    ScoreRaw = r.ScoreRaw,
                    ScoreKde = r.ScoreKde,
                    Relevance = span <= 0 ? 1f : (orderScore(r) - min) / span,
                })
                .ToList(),
            Flavor = parsed == GroupSimilarityFlavor.Kde ? "kde" : "style",
            Model = await _similarityRepo.GetModel(),
        };
    }
}
