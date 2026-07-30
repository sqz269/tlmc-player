using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Common;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public interface IOriginalRepo
{
    Task<List<OriginalWorkReadDto>> GetWorks();
    Task<OriginalWorkReadDto?> GetWork(OriginalWorkId id);
    Task<CursorPage<TrackWithContext>> GetArrangements(OriginalSongId songId, string? cursor, int limit);

    Task<OriginalWorkReadDto> UpsertWork(OriginalWorkWriteDto dto);
    Task<OriginalSongReadDto?> UpsertSong(OriginalWorkId workId, OriginalSongWriteDto dto);

    /// <summary>
    /// Replaces a track's original-song links. Returns the external keys that
    /// resolved to no song (callers answer 400 with them), or null when the track
    /// itself is unknown.
    /// </summary>
    Task<List<string>?> SetTrackOriginals(TrackId trackId, List<string> songExternalKeys);
}

public class OriginalRepo(AppDbContext context) : IOriginalRepo
{
    private readonly AppDbContext _context = context;

    public async Task<List<OriginalWorkReadDto>> GetWorks()
    {
        // The whole vocabulary is a few dozen works; no pagination.
        return await _context.OriginalWorks
            .AsNoTracking()
            .OrderBy(w => w.ExternalKey)
            .Select(w => ProjectWork(w))
            .ToListAsync();
    }

    public async Task<OriginalWorkReadDto?> GetWork(OriginalWorkId id)
    {
        // ProjectWork is a client-side projection over the materialized entity,
        // so the songs must actually be loaded — without the Include it silently
        // projects an empty collection.
        var work = await _context.OriginalWorks
            .AsNoTracking()
            .Include(w => w.Songs)
            .FirstOrDefaultAsync(w => w.Id == id);

        return work == null ? null : ProjectWork(work);
    }

    public async Task<CursorPage<TrackWithContext>> GetArrangements(OriginalSongId songId, string? cursor, int limit)
    {
        var after = Cursor.Decode(cursor, 2);
        var afterSort = after?[0];
        Guid.TryParse(after?[1], out var afterId);
        var song = songId.Value;

        var rows = await _context.Database.SqlQuery<KeysetRow>($@"
            SELECT t.id AS ""Id"", t.name_sort AS ""Sort""
            FROM track t
            WHERE EXISTS (
                    SELECT 1 FROM track_original_song tos
                    WHERE tos.track_id = t.id AND tos.original_song_id = {song})
              AND ({afterSort}::text IS NULL OR (t.name_sort, t.id) > ({afterSort}, {afterId}))
            ORDER BY t.name_sort, t.id
            LIMIT {limit}")
            .ToListAsync();

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
            Items = items.InIdOrder(ids, t => t.Track.Id.Value),
            Next = rows.Count < limit ? null : Cursor.Encode(last.Sort, last.Id.ToString()),
        };
    }

    public async Task<OriginalWorkReadDto> UpsertWork(OriginalWorkWriteDto dto)
    {
        var work = await _context.OriginalWorks
            .Include(w => w.Songs)
            .FirstOrDefaultAsync(w => w.ExternalKey == dto.ExternalKey);

        var isNew = work == null;
        if (work == null)
        {
            work = new OriginalWork
            {
                Id = OriginalWorkId.New(),
                ExternalKey = dto.ExternalKey,
            };
            _context.OriginalWorks.Add(work);
        }

        work.WorkType = dto.WorkType;
        work.FullName = dto.FullName;
        work.ShortName = dto.ShortName;
        work.ExternalRef = dto.ExternalRef;

        // Work titles are denormalized into track search documents, but editing
        // this row does not touch any track — exactly the child-row blind spot
        // the updated_at watermark has (SCHEMA-V6.md section 7). Bump the
        // arranging tracks in the same transaction so the next incremental
        // reindex sees them. A new work has no arrangements to bump.
        var changed = !isNew && _context.Entry(work).Properties.Any(p => p.IsModified);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.SaveChangesAsync();
        if (changed)
        {
            await _context.Database.ExecuteSqlAsync($@"
                UPDATE track SET updated_at = now()
                WHERE id IN (
                    SELECT tos.track_id
                    FROM track_original_song tos
                    JOIN original_song os ON os.id = tos.original_song_id
                    WHERE os.original_work_id = {work.Id.Value})");
        }

        await transaction.CommitAsync();
        return ProjectWork(work);
    }

    public async Task<OriginalSongReadDto?> UpsertSong(OriginalWorkId workId, OriginalSongWriteDto dto)
    {
        var workExists = await _context.OriginalWorks.AnyAsync(w => w.Id == workId);
        if (!workExists)
        {
            return null;
        }

        var song = await _context.OriginalSongs
            .FirstOrDefaultAsync(s => s.ExternalKey == dto.ExternalKey);

        var isNew = song == null;
        if (song == null)
        {
            song = new OriginalSong
            {
                Id = OriginalSongId.New(),
                ExternalKey = dto.ExternalKey,
                OriginalWorkId = workId,
            };
            _context.OriginalSongs.Add(song);
        }
        else
        {
            song.OriginalWorkId = workId;
        }

        song.Title = dto.Title;
        song.TrackIndex = dto.TrackIndex;
        song.ExternalRef = dto.ExternalRef;

        // Same watermark bump as UpsertWork: song titles live inside track
        // search documents, and this row is not the track.
        var changed = !isNew && _context.Entry(song).Properties.Any(p => p.IsModified);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.SaveChangesAsync();
        if (changed)
        {
            await _context.Database.ExecuteSqlAsync($@"
                UPDATE track SET updated_at = now()
                WHERE id IN (
                    SELECT track_id FROM track_original_song
                    WHERE original_song_id = {song.Id.Value})");
        }

        await transaction.CommitAsync();
        return ProjectSong(song);
    }

    public async Task<List<string>?> SetTrackOriginals(TrackId trackId, List<string> songExternalKeys)
    {
        var track = await _context.Tracks
            .Include(t => t.OriginalSongs)
            .FirstOrDefaultAsync(t => t.Id == trackId);
        if (track == null)
        {
            return null;
        }

        var keys = songExternalKeys.Distinct().ToList();
        var songs = await _context.OriginalSongs
            .Where(s => keys.Contains(s.ExternalKey))
            .Select(s => new { s.Id, s.ExternalKey })
            .ToListAsync();

        var unknown = keys.Except(songs.Select(s => s.ExternalKey)).ToList();
        if (unknown.Count > 0)
        {
            return unknown;
        }

        track.OriginalSongs.Clear();
        foreach (var song in songs)
        {
            track.OriginalSongs.Add(new TrackOriginalSong { TrackId = trackId, OriginalSongId = song.Id });
        }

        track.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return [];
    }

    private static OriginalWorkReadDto ProjectWork(OriginalWork w)
    {
        return new OriginalWorkReadDto
        {
            Id = w.Id,
            ExternalKey = w.ExternalKey,
            WorkType = w.WorkType,
            FullName = w.FullName,
            ShortName = w.ShortName,
            ExternalRef = w.ExternalRef,
            Songs = w.Songs
                .OrderBy(s => s.TrackIndex)
                .Select(s => ProjectSong(s))
                .ToList(),
        };
    }

    private static OriginalSongReadDto ProjectSong(OriginalSong s)
    {
        return new OriginalSongReadDto
        {
            Id = s.Id,
            ExternalKey = s.ExternalKey,
            Title = s.Title,
            TrackIndex = s.TrackIndex,
            ExternalRef = s.ExternalRef,
        };
    }
}
