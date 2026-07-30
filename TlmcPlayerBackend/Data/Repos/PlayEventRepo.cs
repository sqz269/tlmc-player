using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public interface IPlayEventRepo
{
    /// <summary>Returns false when the track does not exist.</summary>
    Task<bool> Record(UserId userId, TrackId trackId, PlaySource source, int? msPlayed);

    Task<CursorPage<PlayEventReadDto>> GetHistory(UserId userId, string? cursor, int limit);
}

public class PlayEventRepo(AppDbContext context) : IPlayEventRepo
{
    private readonly AppDbContext _context = context;

    public async Task<bool> Record(UserId userId, TrackId trackId, PlaySource source, int? msPlayed)
    {
        if (!await _context.Tracks.AnyAsync(t => t.Id == trackId))
        {
            return false;
        }

        _context.PlayEvents.Add(new PlayEvent
        {
            UserId = userId,
            TrackId = trackId,
            PlayedAt = DateTime.UtcNow,
            Source = source,
            MsPlayed = msPlayed,
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<CursorPage<PlayEventReadDto>> GetHistory(UserId userId, string? cursor, int limit)
    {
        // Keyset on (played_at DESC, id DESC), matching play_event_user_recent_idx.
        var after = Cursor.Decode(cursor, 2);
        var afterAt = after?[0];
        long.TryParse(after?[1], out var afterId);
        var user = userId.Value;

        var eventIds = await _context.Database.SqlQuery<long>($@"
            SELECT id AS ""Value""
            FROM play_event
            WHERE user_id = {user}
              AND ({afterAt}::timestamptz IS NULL OR (played_at, id) < ({afterAt}::timestamptz, {afterId}))
            ORDER BY played_at DESC, id DESC
            LIMIT {limit}")
            .ToListAsync();

        if (eventIds.Count == 0)
        {
            return new CursorPage<PlayEventReadDto>();
        }

        var rows = await _context.PlayEvents
            .AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                Dto = new PlayEventReadDto
                {
                    PlayedAt = e.PlayedAt,
                    Source = e.Source,
                    MsPlayed = e.MsPlayed,
                    Track = new Dtos.MusicData.TrackListItemDto
                    {
                        Id = e.Track.Id,
                        TrackNumber = e.Track.TrackNumber,
                        Name = e.Track.Name,
                        Duration = e.Track.Duration,
                        HasMedia = e.Track.MediaKey != null,
                        HasLyrics = e.Track.LyricsId != null,
                    },
                    Release = new Dtos.MusicData.ReleaseSlimDto
                    {
                        Id = e.Track.Disc.Release.Id,
                        Name = e.Track.Disc.Release.Name,
                    },
                    ArtworkId = e.Track.Disc.Release.ArtworkId,
                },
            })
            .ToListAsync();

        var order = eventIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var items = rows.OrderBy(r => order[r.Id]).Select(r => r.Dto).ToList();

        var lastRow = rows.OrderBy(r => order[r.Id]).Last();
        return new CursorPage<PlayEventReadDto>
        {
            Items = items,
            Next = eventIds.Count < limit
                ? null
                : Cursor.Encode(lastRow.Dto.PlayedAt.ToString("O"), lastRow.Id.ToString()),
        };
    }
}
