using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Impl.MusicData;

public class CountResult
{
    public long Count { get; set; }
}

public class TrackRepo : ITrackRepo
{
    private readonly AppDbContext _context;

    public TrackRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<Track?> GetTrack(Guid trackId)
    {
        var track = await _context.Tracks.Where(t => t.Id == trackId)
            .Include(t => t.Original)
            .ThenInclude(og => og.Album)
            .Include(t => t.Album)
            .ThenInclude(a => a.Thumbnail)
            .Include(t => t.Album)
            .ThenInclude(a => a.AlbumArtist)
            .Include(t => t.TrackFile)
            .FirstOrDefaultAsync();

        //if (track != null)
        //{
        //    track.Album.Tracks = null;
        //}
        return track;
    }

    public async Task<Tuple<List<Track>, List<Guid>>> GetTracks(IList<Guid> tracks)
    {
        var entities = await _context.Tracks
            .Where(t => tracks.Contains(t.Id))
            .OrderBy(t => t.Id)
            .IgnoreAutoIncludes()
            .Include(t => t.Album.Thumbnail)
            .Include(t => t.Album.AlbumArtist)
            .Include(t => t.Album.Thumbnail.Tiny)
            .Include(t => t.Album.Thumbnail.Small)
            .Include(t => t.Album.Thumbnail.Medium)
            .Include(t => t.Album.Thumbnail.Large)
            .Include(t => t.Album.Thumbnail.Original)
            .Include(t => t.TrackFile)
            .ToListAsync();

        if (entities.Count == tracks.Count)
        {
            return new Tuple<List<Track>, List<Guid>>(entities, new List<Guid>());
        }

        var diff = tracks.Except(
                entities.Select(e => e.Id))
            .ToList();
        return new Tuple<List<Track>, List<Guid>>(entities, diff);
    }

    public async Task<Guid> CreateTrack(Guid albumId, Track track)
    {
        if (track.Id == Guid.Empty)
        {
            track.Id = Guid.NewGuid();
        }

        var album = await _context.Albums.Where(a => a.Id == albumId).FirstOrDefaultAsync();
        if (album == null)
        {
            throw new ArgumentException($"No album found with given Album Id: {albumId}", nameof(albumId));
        }

        track.Album = album;
        album.Tracks.Add(track);
        var addedTrack = await _context.Tracks.AddAsync(track);
        return addedTrack.Entity.Id;
    }

    // We need to validate the track filters before it gets turned into a sql string and applied to the query
    private async Task<bool> ValidateTrackFilters(TrackFilterSelectableRanged filters)
    {
        // Validate all the ids that will be interpolated
        if (filters.CircleIds != null)
        {
            var circles = await _context.Circles
                .Where(c => filters.CircleIds.Contains(c.Id))
                .IgnoreAutoIncludes()
                .AsNoTracking()
                .ToListAsync();
            if (circles.Count != filters.CircleIds.Count)
            {
                throw new ValidationException($"Validation failed for TrackFilters.{nameof(filters.CircleIds)}. Expected: {filters.CircleIds.Count} != {circles.Count}");
            }
        }

        if (filters.OriginalAlbumIds != null)
        {
            var originalAlbumIds = await _context.OriginalAlbums
                .Where(o => filters.OriginalAlbumIds.Contains(o.Id))
                .IgnoreAutoIncludes()
                .AsNoTracking()
                .ToListAsync();
            if (originalAlbumIds.Count != filters.OriginalAlbumIds.Count)
            {
                throw new ValidationException($"Validation failed for TrackFilters.{nameof(filters.OriginalAlbumIds)}. Expected: {filters.OriginalAlbumIds.Count} != {originalAlbumIds.Count}");
            }
        }

        if (filters.OriginalTrackIds != null)
        {
            var originalTrackIds = await _context.OriginalTracks
                .Where(o => filters.OriginalTrackIds.Contains(o.Id))
                .IgnoreAutoIncludes()
                .AsNoTracking()
                .ToListAsync();
            if (originalTrackIds.Count != filters.OriginalTrackIds.Count)
            {
                throw new ValidationException($"Validation failed for TrackFilters.{nameof(filters.OriginalTrackIds)}. Expected: {filters.OriginalTrackIds.Count} != {originalTrackIds.Count}");
            }
        }

        return true;
    }

    private async Task<string> CreateTrackFilterWhereStatement(TrackFilterSelectableRanged filters)
    {
        await ValidateTrackFilters(filters);

        var andConditions = new List<string>();
        var orConditions = new List<string>();
        if (filters.ReleaseDateBegin != null)
        {
            andConditions.Add($"""
                           "ReleaseDate" >= '{filters.ReleaseDateBegin.Value.ToShortDateString()}'::date
                           """);
        }

        if (filters.ReleaseDateEnd != null)
        {
            andConditions.Add($"""
                            "ReleaseDate" <= '{filters.ReleaseDateEnd.Value.ToShortDateString()}'::date
                            """);
        }

        if (filters.CircleIds != null)
        {
            // Transform all the CircleIds to be single quoted
            var idsQuoted = filters.CircleIds.Select(id => $"'{id}'");
            orConditions.Add($"""
                            "CircleIds" && ARRAY [ {string.Join(',', idsQuoted)} ]::uuid[]
                            """);
        }

        if (filters.OriginalAlbumIds != null)
        {
            var idsQuoted = filters.OriginalAlbumIds.Select(id => $"'{id}'");
            orConditions.Add($"""
                            "OriginalAlbumIds" && ARRAY [ {string.Join(',', idsQuoted)} ]
                            """);
        }

        if (filters.OriginalTrackIds != null)
        {
            var idsQuoted = filters.OriginalTrackIds.Select(id => $"'{id}'");
            orConditions.Add($"""
                            "OriginalTrackIds" && ARRAY [ {string.Join(',', idsQuoted)} ]
                            """);
        }

        if (andConditions.Count == 0 && orConditions.Count == 0)
        {
            return "";
        }

        var whereStatement = "WHERE ";
        // Put an AND between the and and or conditions, and parentheses around all of the or conditions
        if (andConditions.Count > 0)
        {
            // Add the and conditions
            var andExpression = string.Join(" AND ", andConditions);
            // Add parentheses around the and conditions
            whereStatement += $"({andExpression})";
        }

        if (orConditions.Count > 0)
        {
            // Add the or conditions
            var orExpression = string.Join(" OR ", orConditions);
            // Add parentheses around the or conditions
            // if we have both and and or conditions, we need to add an AND between them
            if (andConditions.Count > 0)
            {
                whereStatement += $" AND ({orExpression})";
            }
            else
            {
                whereStatement += $"({orExpression})";
            }
        }

        return whereStatement;
    }

    public async Task<IEnumerable<Track>> SampleRandomTrack(int limit, TrackFilterSelectableRanged? filters)
    {
        // Construct where statements
        if (filters == null || filters.IsEmpty())
        {
            return await _context.Tracks
                .FromSqlRaw(@"
                    SELECT *
                    FROM ""Tracks""
                    TABLESAMPLE BERNOULLI(0.1)
                    ORDER BY random()
                    LIMIT {0}", limit)
                .IgnoreAutoIncludes()
                .AsNoTracking()
                .ToListAsync();
        }

        var whereStatement = await CreateTrackFilterWhereStatement(filters);

        var query = $"""
                     SELECT sq.*
                     FROM
                     (
                         SELECT
                             "Tracks".*,
                             "Albums"."ReleaseDate",
                             array_agg(DISTINCT "Circles"."Id") as "CircleIds",
                             array_agg(DISTINCT "OriginalTracks"."Id") as "OriginalTrackIds",
                             array_agg(DISTINCT "OriginalAlbums"."Id") as "OriginalAlbumIds"
                         FROM "Tracks"
                         LEFT JOIN "Albums" on "Tracks"."AlbumId" = "Albums"."Id"
                         LEFT JOIN "AlbumCircle" on "Albums"."Id" = "AlbumCircle"."AlbumsId"
                         LEFT JOIN "Circles" ON "AlbumCircle"."AlbumArtistId" = "Circles"."Id"
                         LEFT JOIN "OriginalTrackTrack" ON "Tracks"."Id" = "OriginalTrackTrack"."TracksId"
                         LEFT JOIN "OriginalTracks" ON "OriginalTrackTrack"."OriginalId" = "OriginalTracks"."Id"
                         LEFT JOIN "OriginalAlbums" ON "OriginalTracks"."AlbumId" = "OriginalAlbums"."Id"
                         GROUP BY "Tracks"."Id", "Albums"."ReleaseDate"
                     ) as sq
                        {whereStatement}
                     ORDER BY random()
                     LIMIT {limit}
                     """;

        return await _context.Tracks
            .FromSqlRaw(query)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> UpdateTrack(Guid trackId, Track track)
    {
        throw new NotImplementedException();
    }

    public async Task<long> GetNumberOfTracksGivenFilter(TrackFilterSelectableRanged? filters)
    {
        if (filters == null || filters.IsEmpty())
        {
            return await _context.Tracks.LongCountAsync();
        }

        var whereStatement = await CreateTrackFilterWhereStatement(filters);
        var query = $"""
                     SELECT count(*)
                     FROM
                     (
                         SELECT
                             "Tracks".*,
                             "Albums"."ReleaseDate",
                             array_agg(DISTINCT "Circles"."Id") as "CircleIds",
                             array_agg(DISTINCT "OriginalTracks"."Id") as "OriginalTrackIds",
                             array_agg(DISTINCT "OriginalAlbums"."Id") as "OriginalAlbumIds"
                         FROM "Tracks"
                         LEFT JOIN "Albums" on "Tracks"."AlbumId" = "Albums"."Id"
                         LEFT JOIN "AlbumCircle" on "Albums"."Id" = "AlbumCircle"."AlbumsId"
                         LEFT JOIN "Circles" ON "AlbumCircle"."AlbumArtistId" = "Circles"."Id"
                         LEFT JOIN "OriginalTrackTrack" ON "Tracks"."Id" = "OriginalTrackTrack"."TracksId"
                         LEFT JOIN "OriginalTracks" ON "OriginalTrackTrack"."OriginalId" = "OriginalTracks"."Id"
                         LEFT JOIN "OriginalAlbums" ON "OriginalTracks"."AlbumId" = "OriginalAlbums"."Id"
                         GROUP BY "Tracks"."Id", "Albums"."ReleaseDate"
                     ) as sq
                        {whereStatement}
                     """;

        var results = _context
            .Set<CountResult>()
            .FromSqlRaw(query)
            .AsEnumerable();  // Execute the query and get the results as an enumerable

        var countResult = results.FirstOrDefault();
        long count = countResult?.Count ?? 0;

        return count;
    }
}