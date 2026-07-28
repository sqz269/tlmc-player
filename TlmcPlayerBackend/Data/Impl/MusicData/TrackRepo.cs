using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils.Extensions;

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

    public async Task<Tuple<IEnumerable<Track>, long>> GetTracksFiltered(
        TrackFilterSelectableRanged? filters,
        int limit,
        int offset,
        TrackOrderOptions options = TrackOrderOptions.Id,
        SortOrder sortOrder = SortOrder.Ascending)
    {
        var trackQueryable = _context.Tracks
            .Include(t => t.Album)
            .ThenInclude(a => a.AlbumArtist)
            .Include(t => t.Album)
            .Include(t => t.Album.Thumbnail)
            .Include(t => t.TrackFile)
            .AsQueryable();

        if (filters != null && !filters.IsEmpty())
        {
            trackQueryable = await CreateTrackFilterEFWhere(filters, trackQueryable);
        }

        // Apply order by and limit
        trackQueryable = options switch
        {
            TrackOrderOptions.Id => trackQueryable.OrderByEx(t => t.Id, sortOrder),
            TrackOrderOptions.Date => trackQueryable.OrderByEx(t => t.Album.ReleaseConvention, sortOrder),
            TrackOrderOptions.Title => trackQueryable.OrderByEx(t => t.Name.Default, sortOrder),
            TrackOrderOptions.Duration => trackQueryable.OrderByEx(t => t.Duration, sortOrder),
            TrackOrderOptions.AlbumId => trackQueryable.OrderByEx(t => t.Album.Id, sortOrder),
            TrackOrderOptions.AlbumTitle => trackQueryable.OrderByEx(t => t.Album.Name.Default, sortOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options, null)
        };

        var result = await trackQueryable
            .Skip(offset)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();

        var count = trackQueryable.LongCount();

        return new Tuple<IEnumerable<Track>, long>(result, count);
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

    private async Task<IQueryable<Track>> CreateTrackFilterEFWhere(TrackFilterSelectableRanged filters, IQueryable<Track> trackQueryable)
    {
        await ValidateTrackFilters(filters);

        if (filters.ReleaseDateBegin != null)
        {
            trackQueryable = trackQueryable.Where(t => t.Album.ReleaseDate >= filters.ReleaseDateBegin);
        }

        if (filters.ReleaseDateEnd != null)
        {
            trackQueryable = trackQueryable.Where(t => t.Album.ReleaseDate <= filters.ReleaseDateEnd);
        }

        if (filters.CircleIds != null)
        {
            trackQueryable = trackQueryable.Where(t => t.Album.AlbumArtist.Any(c => filters.CircleIds.Contains(c.Id)));
        }

        if (filters.OriginalAlbumIds != null)
        {
            trackQueryable = trackQueryable.Where(t => t.Original.Any(o => filters.OriginalAlbumIds.Contains(o.Album.Id)));
        }

        if (filters.OriginalTrackIds != null)
        {
            trackQueryable = trackQueryable.Where(t => t.Original.Any(o => filters.OriginalTrackIds.Contains(o.Id)));
        }

        return trackQueryable;
    }

    private async Task<string> CreateTrackFilterWhereStatement(TrackFilterSelectableRanged? filters)
    {
        if (filters == null)
        {
            return "";
        }

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

    private string CreateStratificationOperator(TrackStratificationMode? stratificationMode) => stratificationMode switch
    {
        TrackStratificationMode.None => "\"TrackId\"",
        TrackStratificationMode.Album => "\"AlbumId\"",
        TrackStratificationMode.Circle => "unnest(\"CircleIds\")",
        null => "\"Id\"",
        _ => throw new ArgumentOutOfRangeException(nameof(stratificationMode), stratificationMode, null),
    };

    public async Task<IEnumerable<Track>> SampleRandomTrack(
        int limit,
        int offset,
        TrackStratificationMode? stratificationMode,
        TrackFilterSelectableRanged? filters,
        double? seed)
    {
        // If no seed is provided, generate a random seed between -1 and 1
        if (seed == null)
        {
            seed = new Random().NextDouble() * 2 - 1;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlAsync($"SELECT setseed({seed})");

        var whereStatement = await CreateTrackFilterWhereStatement(filters);
        var stratificationOperator = CreateStratificationOperator(stratificationMode);
        var cteQuery = $"""
                        WITH AggregatedTracks AS (
                            SELECT
                                "Tracks"."Id" as "TrackId",
                                "Albums"."Id" as "A_AlbumId",
                                "Tracks".*, "Albums"."ReleaseDate",
                                array_agg(DISTINCT "Circles"."Id") AS "CircleIds",
                                array_agg(DISTINCT "OriginalTracks"."Id") AS "OriginalTrackIds",
                                array_agg(DISTINCT "OriginalAlbums"."Id") AS "OriginalAlbumIds"
                            FROM "Tracks"
                            LEFT JOIN "Albums" ON "Tracks"."AlbumId" = "Albums"."Id"
                            LEFT JOIN "AlbumCircle" ON "Albums"."Id" = "AlbumCircle"."AlbumsId"
                            LEFT JOIN "Circles" ON "AlbumCircle"."AlbumArtistId" = "Circles"."Id"
                            LEFT JOIN "OriginalTrackTrack" ON "Tracks"."Id" = "OriginalTrackTrack"."TracksId"
                            LEFT JOIN "OriginalTracks" ON "OriginalTrackTrack"."OriginalId" = "OriginalTracks"."Id"
                            LEFT JOIN "OriginalAlbums" ON "OriginalTracks"."AlbumId" = "OriginalAlbums"."Id"
                            GROUP BY "Tracks"."Id", "A_AlbumId"
                        ),
                        FilteredTracks AS (
                            SELECT *
                            FROM AggregatedTracks
                            {whereStatement}
                        ),
                        StratifiedTracks AS (
                            SELECT
                                *,
                                ROW_NUMBER() OVER (
                                    PARTITION BY {stratificationOperator}
                                    ORDER BY random()
                                ) AS stratified_rank
                            FROM FilteredTracks
                        ),
                        RandomSample AS (
                            SELECT *
                            FROM StratifiedTracks
                            WHERE stratified_rank = 1 -- Select one random track per Circle
                        )
                        SELECT *
                        FROM RandomSample
                        ORDER BY random()
                        LIMIT {limit}
                        OFFSET {offset}
                        """;

        Console.WriteLine(cteQuery);

        var result = await _context.Tracks
            .FromSqlRaw(cteQuery)
            .AsNoTracking()
            .ToListAsync();

        await transaction.CommitAsync();

        return result;
    }

    public async Task<bool> UpdateTrack(Guid trackId, Track track)
    {
        throw new NotImplementedException();
    }

    public async Task<Lyrics?> GetTrackLyrics(Guid trackId)
    {
        var result = await _context.Tracks
                .Where(t => t.Id == trackId)
                .Include(t => t.Lyrics)
                .FirstOrDefaultAsync();

        return result?.Lyrics;
    }

    // should be called internally only
    public async Task<Lyrics?> PutTrackLyrics(Guid lyricsId, Guid trackId, Lyrics lyrics)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Insert new lyrics
            var insertedLyrics = await _context.Lyrics.AddAsync(lyrics);
            var result = await _context.SaveChangesAsync();
            if (result == 0)
            {
                await transaction.RollbackAsync();
                Console.WriteLine("Failed to insert lyrics, save changes returned 0");
                return null;
            }

            // Find track and attach lyrics
            var track = await _context.Tracks
                .Where(t => t.Id == trackId)
                .Include(track => track.Lyrics)
                .FirstOrDefaultAsync();

            if (track == null)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Track not found: {trackId}");
                return null;
            }

            track.Lyrics = insertedLyrics.Entity;
            _context.Tracks.Update(track); // Explicitly mark as modified

            await _context.SaveChangesAsync(); // Persist the relation

            await transaction.CommitAsync();
            return track.Lyrics;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Error inserting lyrics: {ex.Message}");
            return null;
        }
    }


    public async Task<long> GetNumberOfTracksGivenFilter(TrackFilterSelectableRanged? filters)
    {
        if (filters == null || filters.IsEmpty())
        {
            return await _context.Tracks.LongCountAsync();
        }

        var whereStatement = await CreateTrackFilterWhereStatement(filters);
        var query = $"""
                     SELECT *
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

        var results = await _context
            .Tracks
            .FromSqlRaw(query)
            .LongCountAsync();

        long count = results;

        return count;
    }

    public async Task<IEnumerable<(Track Track, double Distance)>> GetSimilarTracks(Guid trackId, int limit, TrackEmbeddingPoolingMode poolingMode)
    {
        var srcTrack = await _context.TrackEmbeddings
            .AsNoTracking()
            .Where(te => te.TrackId == trackId)
            .FirstOrDefaultAsync() ?? throw new Exception($"No embedding found for track with id: {trackId}");

        var neighbors = poolingMode switch
        {
            TrackEmbeddingPoolingMode.Mean => await NearestByMean(srcTrack.EmbeddingMean!, limit),
            TrackEmbeddingPoolingMode.MeanMax => await NearestByMeanMax(srcTrack.EmbeddingMeanMax!, limit),
            _ => throw new ArgumentOutOfRangeException(nameof(poolingMode), poolingMode, null)
        };

        return await HydrateTracks(neighbors);
    }

    public async Task<IEnumerable<(Track Track, double Distance)>> GetSimilarTracks(
        Vector embedding,
        int limit,
        TrackEmbeddingPoolingMode poolingMode)
    {
        var expectedDim = ITrackRepo.EmbeddingDims[poolingMode];
        if (expectedDim != embedding.Memory.Length)
        {
            throw new ArgumentException(
                $"Embedding dimension mismatch. Expected: {expectedDim}, Actual: {embedding.Memory.Length}");
        }

        var neighbors = poolingMode switch
        {
            TrackEmbeddingPoolingMode.Mean => await NearestByMean(embedding, limit),

            // MeanMax is stored as halfvec; an externally supplied fp32 query
            // vector is narrowed to fp16 to match the column
            TrackEmbeddingPoolingMode.MeanMax => await NearestByMeanMax(ToHalfVector(embedding), limit),

            _ => throw new ArgumentOutOfRangeException(nameof(poolingMode), poolingMode, null)
        };

        return await HydrateTracks(neighbors);
    }

    // The ANN query and the metadata hydration are deliberately separate
    // round-trips: pgvector's HNSW index is only considered for a bare
    // `ORDER BY embedding <=> $1 LIMIT n` scan, and dragging the Track/Album/
    // Artist joins into that query invites the planner to fall back to a
    // sequential scan over every embedding.
    private async Task<List<(Guid TrackId, double Distance)>> NearestByMean(Vector embedding, int limit)
    {
        var rows = await _context.TrackEmbeddings
            .AsNoTracking()
            .OrderBy(t => t.EmbeddingMean!.CosineDistance(embedding))
            .Take(limit)
            .Select(t => new { t.TrackId, Distance = t.EmbeddingMean!.CosineDistance(embedding) })
            .ToListAsync();

        return rows.Select(r => (r.TrackId, r.Distance)).ToList();
    }

    private async Task<List<(Guid TrackId, double Distance)>> NearestByMeanMax(HalfVector embedding, int limit)
    {
        var rows = await _context.TrackEmbeddings
            .AsNoTracking()
            .OrderBy(t => t.EmbeddingMeanMax!.CosineDistance(embedding))
            .Take(limit)
            .Select(t => new { t.TrackId, Distance = t.EmbeddingMeanMax!.CosineDistance(embedding) })
            .ToListAsync();

        return rows.Select(r => (r.TrackId, r.Distance)).ToList();
    }

    private async Task<IEnumerable<(Track Track, double Distance)>> HydrateTracks(
        List<(Guid TrackId, double Distance)> neighbors)
    {
        var ids = neighbors.Select(n => n.TrackId).ToList();

        var tracks = await _context.Tracks
            .Where(t => ids.Contains(t.Id))
            .Include(t => t.Album)
            .ThenInclude(a => a.AlbumArtist)
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id);

        return neighbors
            .Where(n => tracks.ContainsKey(n.TrackId))
            .Select(n => (tracks[n.TrackId], n.Distance));
    }

    private static HalfVector ToHalfVector(Vector embedding)
    {
        var floats = embedding.Memory.Span;
        var halves = new Half[floats.Length];
        for (var i = 0; i < floats.Length; i++)
        {
            halves[i] = (Half)floats[i];
        }

        return new HalfVector(halves);
    }
}