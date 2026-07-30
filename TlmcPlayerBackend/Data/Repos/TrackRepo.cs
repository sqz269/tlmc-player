using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public class TrackFilter
{
    public List<CircleId> CircleIds { get; set; } = [];
    public List<OriginalWorkId> OriginalWorkIds { get; set; } = [];
    public List<OriginalSongId> OriginalSongIds { get; set; } = [];
    public DateOnly? ReleaseDateFrom { get; set; }
    public DateOnly? ReleaseDateTo { get; set; }
}

public interface ITrackRepo
{
    Task<TrackReadDto?> GetTrack(TrackId id);
    Task<LyricsReadDto?> GetLyrics(TrackId trackId);
    Task<CursorPage<TrackWithContext>> Filter(TrackFilter filter, string? cursor, int limit);
    Task<List<TrackWithContext>> GetRandom(int count, string? seed);
    Task<CursorPage<TrackWithContext>> GetTracksByCredit(string name, CreditRole? role, string? cursor, int limit);

    /// <summary>Hydrates tracks in the given order; ids that no longer exist are
    /// simply absent (a search index is allowed to be briefly stale).</summary>
    Task<List<TrackWithContext>> GetWithContext(IReadOnlyList<TrackId> ids);
}

public class TrackRepo(AppDbContext context) : ITrackRepo
{
    private readonly AppDbContext _context = context;

    public async Task<TrackReadDto?> GetTrack(TrackId id)
    {
        return await _context.Tracks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TrackReadDto
            {
                Id = t.Id,
                TrackNumber = t.TrackNumber,
                Name = t.Name,
                Duration = t.Duration,
                OriginalNonTouhou = t.OriginalNonTouhou,
                DiscId = t.DiscId,
                DiscNumber = t.Disc.DiscNumber,
                Release = new ReleaseSlimDto { Id = t.Disc.Release.Id, Name = t.Disc.Release.Name },
                ArtworkId = t.Disc.Release.ArtworkId,
                Circles = t.Disc.Release.Circles
                    .OrderBy(rc => rc.Ordinal)
                    .Select(rc => new CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                    .ToList(),
                HasMedia = t.MediaKey != null,
                HlsBitrates = t.HlsBitrates,
                HasDash = t.HasDash,
                Credits = t.Credits
                    .OrderBy(c => c.Role).ThenBy(c => c.Ordinal)
                    .Select(c => new TrackCreditDto { Role = c.Role, Name = c.CreditName })
                    .ToList(),
                Tags = t.Tags
                    .Select(tt => new TagDto { Id = tt.TagId, Name = tt.Tag.Name })
                    .ToList(),
                OriginalSongs = t.OriginalSongs
                    .Select(ts => new OriginalSongSlimDto { Id = ts.OriginalSongId, Title = ts.OriginalSong.Title })
                    .ToList(),
                HasLyrics = t.LyricsId != null,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<LyricsReadDto?> GetLyrics(TrackId trackId)
    {
        return await _context.Tracks
            .AsNoTracking()
            .Where(t => t.Id == trackId && t.Lyrics != null)
            .Select(t => new LyricsReadDto
            {
                Id = t.Lyrics!.Id,
                Variants = t.Lyrics.Variants,
                ReferenceUrl = t.Lyrics.ReferenceUrl,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<CursorPage<TrackWithContext>> Filter(TrackFilter filter, string? cursor, int limit)
    {
        // Keyset pagination needs the row-value comparison (name_sort, id) > (...),
        // which LINQ cannot express over a value-converted id. One raw statement
        // with guarded parameters covers every filter combination; empty arrays and
        // null casts disable the clauses they guard.
        var (afterSort, afterId) = DecodeAfter(cursor);
        var circleIds = filter.CircleIds.Select(c => c.Value).ToArray();
        var workIds = filter.OriginalWorkIds.Select(w => w.Value).ToArray();
        var songIds = filter.OriginalSongIds.Select(s => s.Value).ToArray();
        var dateFrom = filter.ReleaseDateFrom?.ToString("O");
        var dateTo = filter.ReleaseDateTo?.ToString("O");

        var rows = await _context.Database.SqlQuery<KeysetRow>($@"
            SELECT t.id AS ""Id"", t.name_sort AS ""Sort""
            FROM track t
            JOIN disc d ON d.id = t.disc_id
            JOIN release r ON r.id = d.release_id
            WHERE (cardinality({circleIds}) = 0 OR EXISTS (
                    SELECT 1 FROM release_circle rc
                    WHERE rc.release_id = r.id AND rc.circle_id = ANY({circleIds})))
              AND (cardinality({workIds}) = 0 OR EXISTS (
                    SELECT 1 FROM track_original_song tos
                    JOIN original_song os ON os.id = tos.original_song_id
                    WHERE tos.track_id = t.id AND os.original_work_id = ANY({workIds})))
              AND (cardinality({songIds}) = 0 OR EXISTS (
                    SELECT 1 FROM track_original_song tos2
                    WHERE tos2.track_id = t.id AND tos2.original_song_id = ANY({songIds})))
              AND ({dateFrom}::date IS NULL OR r.release_date >= {dateFrom}::date)
              AND ({dateTo}::date IS NULL OR r.release_date <= {dateTo}::date)
              AND ({afterSort}::text IS NULL OR (t.name_sort, t.id) > ({afterSort}, {afterId}))
            ORDER BY t.name_sort, t.id
            LIMIT {limit}")
            .ToListAsync();

        return await HydratePage(rows, limit);
    }

    public async Task<List<TrackWithContext>> GetRandom(int count, string? seed)
    {
        // setseed + random() must share one connection, hence the transaction.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var seedValue = Math.Clamp(SeedUtils.GetSeed(seed), -1.0, 1.0);
        await _context.Database.ExecuteSqlAsync($"SELECT setseed({seedValue})");

        var ids = await _context.Database
            .SqlQuery<Guid>($@"SELECT id AS ""Value"" FROM track WHERE media_key IS NOT NULL ORDER BY random() LIMIT {count}")
            .ToListAsync();

        await transaction.CommitAsync();

        var typedIds = ids.Select(g => new TrackId(g)).ToList();
        var rows = await _context.Tracks
            .AsNoTracking()
            .Where(t => typedIds.Contains(t.Id))
            .ToTrackWithContext()
            .ToListAsync();

        return rows.InIdOrder(ids, r => r.Track.Id.Value);
    }

    public async Task<CursorPage<TrackWithContext>> GetTracksByCredit(string name, CreditRole? role, string? cursor, int limit)
    {
        var (afterSort, afterId) = DecodeAfter(cursor);
        var needle = name.ToLowerInvariant();
        // Postgres enum labels are the snake_case member names; every CreditRole
        // member is a single word, so lowercasing is the full translation.
        var roleLabel = role?.ToString().ToLowerInvariant();

        var rows = await _context.Database.SqlQuery<KeysetRow>($@"
            SELECT t.id AS ""Id"", t.name_sort AS ""Sort""
            FROM track t
            WHERE EXISTS (
                    SELECT 1 FROM track_credit tc
                    WHERE tc.track_id = t.id
                      AND tc.credit_name_sort = {needle}
                      AND ({roleLabel}::credit_role IS NULL OR tc.role = {roleLabel}::credit_role))
              AND ({afterSort}::text IS NULL OR (t.name_sort, t.id) > ({afterSort}, {afterId}))
            ORDER BY t.name_sort, t.id
            LIMIT {limit}")
            .ToListAsync();

        return await HydratePage(rows, limit);
    }

    public async Task<List<TrackWithContext>> GetWithContext(IReadOnlyList<TrackId> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await _context.Tracks
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToTrackWithContext()
            .ToListAsync();

        return rows.InIdOrder(ids.Select(i => i.Value).ToList(), r => r.Track.Id.Value);
    }

    private static (string? Sort, Guid Id) DecodeAfter(string? cursor)
    {
        var after = Cursor.Decode(cursor, 2);
        if (after == null || !Guid.TryParse(after[1], out var afterId))
        {
            return (null, Guid.Empty);
        }

        return (after[0], afterId);
    }

    private async Task<CursorPage<TrackWithContext>> HydratePage(List<KeysetRow> rows, int limit)
    {
        if (rows.Count == 0)
        {
            return new CursorPage<TrackWithContext>();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var typedIds = ids.Select(g => new TrackId(g)).ToList();

        var items = await _context.Tracks
            .AsNoTracking()
            .Where(t => typedIds.Contains(t.Id))
            .ToTrackWithContext()
            .ToListAsync();

        var last = rows[^1];
        return new CursorPage<TrackWithContext>
        {
            Items = items.InIdOrder(ids, r => r.Track.Id.Value),
            Next = rows.Count < limit ? null : Cursor.Encode(last.Sort, last.Id.ToString()),
        };
    }
}
