using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.Internal;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// The ETL's write surface. Every action requires the X-Internal-Api-Key header.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalController(AppDbContext context, IOriginalRepo originalRepo) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly IOriginalRepo _originalRepo = originalRepo;

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

        return unknown.Count > 0
            ? BadRequest(new { unknown_song_external_keys = unknown })
            : NoContent();
    }
}
