using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public interface ICircleRepo
{
    Task<CursorPage<CircleReadDto>> GetCircles(string? cursor, int limit);
    Task<CircleReadDto?> GetCircle(CircleId id);
    Task<CircleReadDto?> GetCircleByName(string name);
}

public class CircleRepo(AppDbContext context) : ICircleRepo
{
    private readonly AppDbContext _context = context;

    public async Task<CursorPage<CircleReadDto>> GetCircles(string? cursor, int limit)
    {
        var after = Cursor.Decode(cursor, 2);
        var afterSort = after?[0];
        Guid.TryParse(after?[1], out var afterId);

        var rows = await _context.Database.SqlQuery<KeysetRow>($@"
            SELECT id AS ""Id"", name AS ""Sort""
            FROM circle
            WHERE ({afterSort}::text IS NULL OR (name, id) > ({afterSort}, {afterId}))
            ORDER BY name, id
            LIMIT {limit}")
            .ToListAsync();

        if (rows.Count == 0)
        {
            return new CursorPage<CircleReadDto>();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var typedIds = ids.Select(g => new CircleId(g)).ToList();

        var items = await ProjectCircles(_context.Circles.AsNoTracking().Where(c => typedIds.Contains(c.Id)));

        var last = rows[^1];
        return new CursorPage<CircleReadDto>
        {
            Items = items.InIdOrder(ids, c => c.Id.Value),
            Next = rows.Count < limit ? null : Cursor.Encode(last.Sort, last.Id.ToString()),
        };
    }

    public async Task<CircleReadDto?> GetCircle(CircleId id)
    {
        var items = await ProjectCircles(_context.Circles.AsNoTracking().Where(c => c.Id == id));
        return items.FirstOrDefault();
    }

    public async Task<CircleReadDto?> GetCircleByName(string name)
    {
        var items = await ProjectCircles(_context.Circles.AsNoTracking()
            .Where(c => c.Name == name || c.Alias.Contains(name)));
        return items.FirstOrDefault();
    }

    private static Task<List<CircleReadDto>> ProjectCircles(IQueryable<Models.MusicData.Circle> query)
    {
        return query
            .Select(c => new CircleReadDto
            {
                Id = c.Id,
                Name = c.Name,
                Status = c.Status,
                Established = c.Established,
                Country = c.Country,
                Alias = c.Alias,
                Website = c.Website
                    .Select(w => new CircleWebsiteDto { Url = w.Url, Invalid = w.Invalid })
                    .ToList(),
            })
            .ToListAsync();
    }
}
