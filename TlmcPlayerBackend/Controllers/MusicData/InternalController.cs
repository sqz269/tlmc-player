using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Internal;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Search;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// The ETL's write surface. Every action requires the X-Internal-Api-Key header.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalController(
    AppDbContext context,
    IOriginalRepo originalRepo,
    ISearchIndexService search) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly IOriginalRepo _originalRepo = originalRepo;
    private readonly ISearchIndexService _search = search;

    /// <summary>Upserts extended circle metadata by exact circle name (the thwiki pass).</summary>
    [HttpPut("circle")]
    [InternalApiKey]
    public async Task<ActionResult<object>> UpsertCircles([FromBody] List<CircleUpsertDto> dtos)
    {
        var updated = 0;
        var created = 0;

        foreach (var dto in dtos)
        {
            var circle = await _context.Circles
                .Include(c => c.Website)
                .FirstOrDefaultAsync(c => c.Name == dto.Name || c.Alias.Contains(dto.Name));

            if (circle == null)
            {
                circle = new Circle { Id = CircleId.New(), Name = dto.Name };
                _context.Circles.Add(circle);
                created++;
            }
            else
            {
                updated++;
            }

            if (dto.Status is { } status)
            {
                circle.Status = status;
            }

            circle.Established = dto.Established ?? circle.Established;
            circle.Country = dto.Country ?? circle.Country;

            if (dto.Alias is { } alias)
            {
                circle.Alias = circle.Alias.Union(alias).ToList();
            }

            if (dto.Websites is { } websites)
            {
                circle.Website.Clear();
                foreach (var site in websites)
                {
                    circle.Website.Add(new CircleWebsite
                    {
                        Id = Guid.CreateVersion7(),
                        CircleId = circle.Id,
                        Url = site.Url,
                        Invalid = site.Invalid,
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        return new { created, updated };
    }

    /// <summary>Creates or replaces a track's lyrics document.</summary>
    [HttpPut("track/{trackId}/lyrics")]
    [InternalApiKey]
    public async Task<ActionResult<LyricsReadDto>> PutTrackLyrics(TrackId trackId, [FromBody] LyricsWriteDto dto)
    {
        var track = await _context.Tracks
            .Include(t => t.Lyrics)
            .FirstOrDefaultAsync(t => t.Id == trackId);
        if (track == null)
        {
            return NotFound($"Track {trackId} does not exist");
        }

        if (track.Lyrics == null)
        {
            track.Lyrics = new Lyrics
            {
                Id = LyricsId.New(),
                Variants = dto.Variants,
                ReferenceUrl = dto.ReferenceUrl,
            };
        }
        else
        {
            track.Lyrics.Variants = dto.Variants;
            track.Lyrics.ReferenceUrl = dto.ReferenceUrl;
        }

        track.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Best-effort: the write is committed either way, and the bumped
        // updated_at watermark repairs a missed push on the next reindex.
        await _search.TryIndexTracksAsync([trackId], HttpContext.RequestAborted);

        return new LyricsReadDto
        {
            Id = track.Lyrics.Id,
            Variants = track.Lyrics.Variants,
            ReferenceUrl = track.Lyrics.ReferenceUrl,
        };
    }

    /// <summary>
    /// Replaces a track's original-song links. The endpoint v5 left as
    /// NotImplementedException — the thwiki pipeline cannot land without it.
    /// </summary>
    [HttpPut("track/{trackId}/originals")]
    [InternalApiKey]
    public async Task<IActionResult> PutTrackOriginals(TrackId trackId, [FromBody] TrackOriginalsWriteDto dto)
    {
        var unknown = await _originalRepo.SetTrackOriginals(trackId, dto.SongExternalKeys);
        if (unknown == null)
        {
            return NotFound($"Track {trackId} does not exist");
        }

        if (unknown.Count > 0)
        {
            return BadRequest(new { unknown_song_external_keys = unknown });
        }

        await _search.TryIndexTracksAsync([trackId], HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>
    /// Rebuilds the search index from Postgres — the database is the source of
    /// truth and the index is a projection that must be reconstructible at any
    /// time (SCHEMA-V6.md section 7). Without `since`: a full rebuild into a
    /// staging index swapped in atomically. With `since`: an incremental upsert
    /// of tracks whose updated_at passed the watermark — the ETL calls this
    /// after a load with `since` set to the moment the load started.
    ///
    /// Synchronous by design: the caller is the ETL, and it wants to know the
    /// index is consistent before declaring the load done. Use a generous client
    /// timeout for full rebuilds.
    /// </summary>
    [HttpPost("search/reindex")]
    [InternalApiKey]
    public async Task<ActionResult<object>> ReindexSearch(
        [FromQuery] DateTimeOffset? since, CancellationToken ct)
    {
        if (!_search.Enabled)
        {
            return Problem(
                title: "Search is not available",
                detail: "No search engine is configured for this deployment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await _search.EnsureIndexAsync(ct);
            var indexed = await _search.ReindexAsync(since?.UtcDateTime, ct);
            return new { indexed, since, elapsed_ms = stopwatch.ElapsedMilliseconds };
        }
        catch (MeiliUnavailableException e)
        {
            return Problem(
                title: "Search engine unreachable",
                detail: e.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Replaces a track's credit rows for the roles present in the payload.
    /// The loader's verbatim staff rows survive because they use a role the
    /// thwiki pass never sends.
    /// </summary>
    [HttpPut("track/{trackId}/credits")]
    [InternalApiKey]
    public async Task<IActionResult> PutTrackCredits(TrackId trackId, [FromBody] TrackCreditsWriteDto dto)
    {
        var track = await _context.Tracks.FirstOrDefaultAsync(t => t.Id == trackId);
        if (track == null)
        {
            return NotFound($"Track {trackId} does not exist");
        }

        // Duplicate role groups merge in payload order; blanks drop; repeats collapse.
        var byRole = dto.Credits
            .GroupBy(g => g.Role)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(x => x.Names)
                    .Select(n => n?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList());

        var roles = byRole.Keys.ToList();

        await using var tx = await _context.Database.BeginTransactionAsync();

        // ExecuteDelete keeps the change tracker out of it: the fresh rows may
        // reuse the same (track_id, role, ordinal) keys the old ones held.
        await _context.TrackCredits
            .Where(tc => tc.TrackId == trackId && roles.Contains(tc.Role))
            .ExecuteDeleteAsync();

        foreach (var (role, names) in byRole)
        {
            short ordinal = 0;
            foreach (var name in names)
            {
                _context.TrackCredits.Add(new TrackCredit
                {
                    TrackId = trackId,
                    Role = role,
                    Ordinal = ordinal++,
                    CreditName = name!,
                });
            }
        }

        track.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    /// <summary>Enriches a release from an external source (the thwiki pass).</summary>
    [HttpPut("release/{releaseId}/source-meta")]
    [InternalApiKey]
    public async Task<IActionResult> PutReleaseSourceMeta(ReleaseId releaseId, [FromBody] ReleaseSourceMetaWriteDto dto)
    {
        var release = await _context.Releases.FirstOrDefaultAsync(r => r.Id == releaseId);
        if (release == null)
        {
            return NotFound($"Release {releaseId} does not exist");
        }

        if (dto.CatalogNumber is { } catalog && string.IsNullOrEmpty(release.CatalogNumber))
        {
            release.CatalogNumber = catalog;
        }

        // Reassign rather than mutate: array columns change-detect by reference.
        if (dto.Website is { } site && !release.Websites.Contains(site))
        {
            release.Websites = [.. release.Websites, site];
        }

        if (dto.DataSource is { } source && !release.DataSources.Contains(source))
        {
            release.DataSources = [.. release.DataSources, source];
        }

        release.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
