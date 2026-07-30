using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Subsonic;

public enum AlbumListType
{
    AlphabeticalByName,
    Newest,
    ByYear,
    Random,
}

/// <summary>Row shape for the facade's album lists; ids stay raw Guids until mapping.</summary>
public class AlbumRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? ReleaseDate { get; set; }
    public Guid? ArtworkId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ArtistId { get; set; }
    public string? Artist { get; set; }
    public string? DisplayArtist { get; set; }
    public int SongCount { get; set; }
    public int DurationSec { get; set; }
}

public class ArtistRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int AlbumCount { get; set; }
}

/// <summary>
/// The facade's read layer (Docs/SUBSONIC.md section 8). Subsonic paging is
/// offset/size over stable orderings — a different shape from the native cursor
/// repos, so it lives here instead of contorting them. `ORDER BY name_sort, id`
/// rides the existing indexes; OFFSET at ≤500-row pages is fine at this scale.
/// </summary>
public class SubsonicQueries(AppDbContext context)
{
    // Every album list shares this projection; variants append WHERE/ORDER/paging
    // with {n} placeholders through SqlQueryRaw, which parameterizes them.
    private const string AlbumSelect = """
        SELECT r.id AS "Id",
               r.name->>'default' AS "Name",
               r.release_date AS "release_date",
               r.artwork_id AS "artwork_id",
               r.created_at AS "created_at",
               fc.circle_id AS "artist_id",
               fc.name AS "Artist",
               (SELECT string_agg(c.name, ', ' ORDER BY rc.ordinal)
                FROM release_circle rc JOIN circle c ON c.id = rc.circle_id
                WHERE rc.release_id = r.id) AS "display_artist",
               (SELECT count(*)::int FROM disc d JOIN track t ON t.disc_id = d.id
                WHERE d.release_id = r.id) AS "song_count",
               (SELECT coalesce(extract(epoch FROM sum(t.duration)), 0)::int
                FROM disc d JOIN track t ON t.disc_id = d.id
                WHERE d.release_id = r.id) AS "duration_sec"
        FROM release r
        LEFT JOIN LATERAL (
            SELECT rc.circle_id, c.name
            FROM release_circle rc JOIN circle c ON c.id = rc.circle_id
            WHERE rc.release_id = r.id
            ORDER BY rc.ordinal LIMIT 1) fc ON true
        """;

    private readonly AppDbContext _context = context;

    public Task<List<AlbumRow>> GetAlbumList(
        AlbumListType type, int size, int offset, int fromYear, int toYear)
    {
        return type switch
        {
            AlbumListType.AlphabeticalByName => _context.Database
                .SqlQueryRaw<AlbumRow>(
                    AlbumSelect + " ORDER BY r.name_sort, r.id OFFSET {0} LIMIT {1}",
                    offset, size)
                .ToListAsync(),

            AlbumListType.Newest => _context.Database
                .SqlQueryRaw<AlbumRow>(
                    AlbumSelect + " ORDER BY r.release_date DESC NULLS LAST, r.id DESC OFFSET {0} LIMIT {1}",
                    offset, size)
                .ToListAsync(),

            // fromYear > toYear means "newest first" per the spec.
            AlbumListType.ByYear when fromYear <= toYear => _context.Database
                .SqlQueryRaw<AlbumRow>(
                    AlbumSelect + """
                     WHERE r.release_date IS NOT NULL
                       AND extract(year FROM r.release_date) BETWEEN {0} AND {1}
                     ORDER BY r.release_date, r.id OFFSET {2} LIMIT {3}
                    """,
                    fromYear, toYear, offset, size)
                .ToListAsync(),

            AlbumListType.ByYear => _context.Database
                .SqlQueryRaw<AlbumRow>(
                    AlbumSelect + """
                     WHERE r.release_date IS NOT NULL
                       AND extract(year FROM r.release_date) BETWEEN {0} AND {1}
                     ORDER BY r.release_date DESC, r.id DESC OFFSET {2} LIMIT {3}
                    """,
                    toYear, fromYear, offset, size)
                .ToListAsync(),

            AlbumListType.Random => _context.Database
                .SqlQueryRaw<AlbumRow>(
                    AlbumSelect + " ORDER BY random() LIMIT {0}", size)
                .ToListAsync(),

            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public Task<List<AlbumRow>> GetAlbumsByCircle(CircleId circleId)
    {
        return _context.Database
            .SqlQueryRaw<AlbumRow>(
                AlbumSelect + """
                 WHERE EXISTS (SELECT 1 FROM release_circle rc
                               WHERE rc.release_id = r.id AND rc.circle_id = {0})
                 ORDER BY r.release_date NULLS LAST, r.name_sort, r.id
                """,
                circleId.Value)
            .ToListAsync();
    }

    public Task<List<AlbumRow>> SearchAlbums(string query, int count, int offset)
    {
        return _context.Database
            .SqlQueryRaw<AlbumRow>(
                AlbumSelect + """
                 WHERE ({0} = '' OR r.name->>'default' ILIKE {1}
                        OR r.name->>'en' ILIKE {1} OR r.name->>'jp' ILIKE {1}
                        OR r.name->>'zh' ILIKE {1})
                 ORDER BY r.name_sort, r.id OFFSET {2} LIMIT {3}
                """,
                query, LikePattern(query), offset, count)
            .ToListAsync();
    }

    /// <summary>Circles with at least one release, A–Z by name; the getArtists index.</summary>
    public Task<List<ArtistRow>> GetArtistIndex()
    {
        return _context.Database
            .SqlQueryRaw<ArtistRow>("""
                SELECT c.id AS "Id", c.name AS "Name", count(rc.release_id)::int AS "album_count"
                FROM circle c JOIN release_circle rc ON rc.circle_id = c.id
                GROUP BY c.id, c.name
                ORDER BY lower(c.name), c.id
                """)
            .ToListAsync();
    }

    public async Task<ArtistRow?> GetArtist(CircleId circleId)
    {
        var rows = await _context.Database
            .SqlQueryRaw<ArtistRow>("""
                SELECT c.id AS "Id", c.name AS "Name",
                       (SELECT count(*)::int FROM release_circle rc
                        WHERE rc.circle_id = c.id) AS "album_count"
                FROM circle c WHERE c.id = {0}
                """,
                circleId.Value)
            .ToListAsync();
        return rows.FirstOrDefault();
    }

    public Task<List<ArtistRow>> SearchArtists(string query, int count, int offset)
    {
        return _context.Database
            .SqlQueryRaw<ArtistRow>("""
                SELECT c.id AS "Id", c.name AS "Name", count(rc.release_id)::int AS "album_count"
                FROM circle c JOIN release_circle rc ON rc.circle_id = c.id
                WHERE ({0} = '' OR c.name ILIKE {1}
                       OR EXISTS (SELECT 1 FROM unnest(c.alias) al WHERE al ILIKE {1}))
                GROUP BY c.id, c.name
                ORDER BY lower(c.name), c.id OFFSET {2} LIMIT {3}
                """,
                query, LikePattern(query), offset, count)
            .ToListAsync();
    }

    /// <summary>
    /// Song search without the engine: substring match over name_sort. Also serves
    /// the empty-query full-library sync (Docs/SUBSONIC.md section 7), which must
    /// come from Postgres — deterministic order, never a stale index.
    /// </summary>
    public Task<List<TrackWithContext>> SearchSongsInPostgres(string query, int count, int offset)
    {
        var tracks = _context.Tracks.AsNoTracking();
        if (query.Length > 0)
        {
            var pattern = LikePattern(query);
            tracks = tracks.Where(t => EF.Functions.ILike(t.NameSort!, pattern, "\\"));
        }

        return tracks
            .OrderBy(t => t.NameSort).ThenBy(t => t.Id)
            .Skip(offset).Take(count)
            .ToTrackWithContext()
            .ToListAsync();
    }

    private static string LikePattern(string query)
    {
        var escaped = query
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
