using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public enum ReleaseSort
{
    /// <summary>name_sort ascending.</summary>
    Name,

    /// <summary>release_date descending, undated releases last.</summary>
    Date,
}

public interface IReleaseRepo
{
    Task<CursorPage<ReleaseListItemDto>> GetReleases(ReleaseSort sort, string? cursor, int limit);
    Task<ReleaseReadDto?> GetRelease(ReleaseId id);
    Task<CursorPage<ReleaseListItemDto>> GetReleasesByCircle(CircleId circleId, string? cursor, int limit);
}

public class ReleaseRepo(AppDbContext context) : IReleaseRepo
{
    private readonly AppDbContext _context = context;

    public async Task<CursorPage<ReleaseListItemDto>> GetReleases(ReleaseSort sort, string? cursor, int limit)
    {
        // Keyset pagination uses row-value comparison, which LINQ cannot express;
        // snake_case identifiers make the raw form read like the DDL. Ids come back
        // first, then one LINQ hydration pass projects the page.
        var after = Cursor.Decode(cursor, 2);
        var afterSort = after?[0];
        Guid.TryParse(after?[1], out var afterId);

        FormattableString sql = sort switch
        {
            ReleaseSort.Name when after == null =>
                $@"SELECT id AS ""Id"", name_sort AS ""Sort"" FROM release
                   ORDER BY name_sort, id LIMIT {limit}",
            ReleaseSort.Name =>
                $@"SELECT id AS ""Id"", name_sort AS ""Sort"" FROM release
                   WHERE (name_sort, id) > ({afterSort}, {afterId})
                   ORDER BY name_sort, id LIMIT {limit}",
            ReleaseSort.Date when after == null =>
                $@"SELECT id AS ""Id"", coalesce(release_date, '-infinity'::date)::text AS ""Sort"" FROM release
                   ORDER BY coalesce(release_date, '-infinity'::date) DESC, id DESC LIMIT {limit}",
            ReleaseSort.Date =>
                $@"SELECT id AS ""Id"", coalesce(release_date, '-infinity'::date)::text AS ""Sort"" FROM release
                   WHERE (coalesce(release_date, '-infinity'::date), id) < ({afterSort}::date, {afterId})
                   ORDER BY coalesce(release_date, '-infinity'::date) DESC, id DESC LIMIT {limit}",
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };

        var rows = await _context.Database.SqlQuery<KeysetRow>(sql).ToListAsync();
        return await HydratePage(rows, limit);
    }

    public async Task<ReleaseReadDto?> GetRelease(ReleaseId id)
    {
        return await _context.Releases
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReleaseReadDto
            {
                Id = r.Id,
                Name = r.Name,
                ReleaseDate = r.ReleaseDate,
                ReleaseConvention = r.ReleaseConvention,
                CatalogNumber = r.CatalogNumber,
                Websites = r.Websites,
                DataSources = r.DataSources,
                Circles = r.Circles
                    .OrderBy(rc => rc.Ordinal)
                    .Select(rc => new CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                    .ToList(),
                Artwork = r.Artwork == null
                    ? null
                    : new ArtworkReadDto
                    {
                        Id = r.Artwork.Id,
                        Colors = r.Artwork.Colors,
                        Variants = r.Artwork.Variants
                            .OrderBy(v => v.SizePx)
                            .Select(v => new ArtworkVariantDto { SizePx = v.SizePx, AssetId = v.AssetId })
                            .ToList(),
                    },
                Discs = r.Discs
                    .OrderBy(d => d.DiscNumber)
                    .Select(d => new DiscReadDto
                    {
                        Id = d.Id,
                        DiscNumber = d.DiscNumber,
                        Name = d.Name,
                        Tracks = d.Tracks
                            .OrderBy(t => t.TrackNumber)
                            .Select(t => new TrackListItemDto
                            {
                                Id = t.Id,
                                TrackNumber = t.TrackNumber,
                                Name = t.Name,
                                Duration = t.Duration,
                                HasMedia = t.MediaKey != null,
                                HasLyrics = t.LyricsId != null,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync();
    }

    public async Task<CursorPage<ReleaseListItemDto>> GetReleasesByCircle(CircleId circleId, string? cursor, int limit)
    {
        var after = Cursor.Decode(cursor, 2);
        var afterSort = after?[0];
        Guid.TryParse(after?[1], out var afterId);
        var circle = circleId.Value;

        FormattableString sql =
            $@"SELECT r.id AS ""Id"", coalesce(r.release_date, '-infinity'::date)::text AS ""Sort""
               FROM release r
               WHERE EXISTS (SELECT 1 FROM release_circle rc WHERE rc.release_id = r.id AND rc.circle_id = {circle})
                 AND ({afterSort}::text IS NULL OR (coalesce(r.release_date, '-infinity'::date), r.id) < ({afterSort}::date, {afterId}))
               ORDER BY coalesce(r.release_date, '-infinity'::date) DESC, r.id DESC LIMIT {limit}";

        var rows = await _context.Database.SqlQuery<KeysetRow>(sql).ToListAsync();
        return await HydratePage(rows, limit);
    }

    private async Task<CursorPage<ReleaseListItemDto>> HydratePage(List<KeysetRow> rows, int limit)
    {
        if (rows.Count == 0)
        {
            return new CursorPage<ReleaseListItemDto>();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var typedIds = ids.Select(g => new ReleaseId(g)).ToList();

        var items = await _context.Releases
            .AsNoTracking()
            .Where(r => typedIds.Contains(r.Id))
            .Select(r => new ReleaseListItemDto
            {
                Id = r.Id,
                Name = r.Name,
                ReleaseDate = r.ReleaseDate,
                ReleaseConvention = r.ReleaseConvention,
                CatalogNumber = r.CatalogNumber,
                ArtworkId = r.ArtworkId,
                TrackCount = r.Discs.SelectMany(d => d.Tracks).Count(),
                Circles = r.Circles
                    .OrderBy(rc => rc.Ordinal)
                    .Select(rc => new CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                    .ToList(),
            })
            .ToListAsync();

        var last = rows[^1];
        return new CursorPage<ReleaseListItemDto>
        {
            Items = items.InIdOrder(ids, r => r.Id.Value),
            Next = rows.Count < limit ? null : Cursor.Encode(last.Sort, last.Id.ToString()),
        };
    }
}
