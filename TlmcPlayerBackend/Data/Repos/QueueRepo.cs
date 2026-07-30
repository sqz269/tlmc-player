using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Data.Repos;

public interface IQueueRepo
{
    Task<List<QueueItemDto>> GetQueue(UserId userId);

    /// <summary>Replaces the whole queue. Returns unknown track ids (callers answer 400).</summary>
    Task<List<TrackId>> Replace(UserId userId, List<TrackId> trackIds);

    /// <summary>Appends, or inserts at the front ("play next"). Duplicates are fine —
    /// the same track queued twice is two entries.</summary>
    Task<List<TrackId>> Enqueue(UserId userId, List<TrackId> trackIds, bool atFront);

    Task<bool> RemoveItem(UserId userId, long itemId);
    Task Clear(UserId userId);
}

public class QueueRepo(AppDbContext context) : IQueueRepo
{
    private readonly AppDbContext _context = context;

    public Task<List<QueueItemDto>> GetQueue(UserId userId)
    {
        return _context.QueueItems
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.Position)
            .Select(q => new QueueItemDto
            {
                Id = q.Id,
                Position = q.Position,
                EnqueuedAt = q.EnqueuedAt,
                Track = new Dtos.MusicData.TrackListItemDto
                {
                    Id = q.Track.Id,
                    TrackNumber = q.Track.TrackNumber,
                    Name = q.Track.Name,
                    Duration = q.Track.Duration,
                    HasMedia = q.Track.MediaKey != null,
                    HasLyrics = q.Track.LyricsId != null,
                },
                Release = new Dtos.MusicData.ReleaseSlimDto
                {
                    Id = q.Track.Disc.Release.Id,
                    Name = q.Track.Disc.Release.Name,
                },
                ArtworkId = q.Track.Disc.Release.ArtworkId,
            })
            .ToListAsync();
    }

    public async Task<List<TrackId>> Replace(UserId userId, List<TrackId> trackIds)
    {
        var unknown = await UnknownTracks(trackIds);
        if (unknown.Count > 0)
        {
            return unknown;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.QueueItems.Where(q => q.UserId == userId).ExecuteDeleteAsync();

        var position = 1;
        foreach (var trackId in trackIds)
        {
            _context.QueueItems.Add(new QueueItem
            {
                UserId = userId,
                TrackId = trackId,
                Position = position++,
                EnqueuedAt = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return [];
    }

    public async Task<List<TrackId>> Enqueue(UserId userId, List<TrackId> trackIds, bool atFront)
    {
        var unknown = await UnknownTracks(trackIds);
        if (unknown.Count > 0)
        {
            return unknown;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var existing = await _context.QueueItems
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.Position)
            .ToListAsync();

        if (atFront)
        {
            // The position unique is DEFERRABLE, so the shift and the inserts
            // commit as one consistent renumbering.
            foreach (var item in existing)
            {
                item.Position += trackIds.Count;
            }

            var position = 1;
            foreach (var trackId in trackIds)
            {
                _context.QueueItems.Add(new QueueItem
                {
                    UserId = userId,
                    TrackId = trackId,
                    Position = position++,
                    EnqueuedAt = DateTime.UtcNow,
                });
            }
        }
        else
        {
            var position = existing.Count == 0 ? 1 : existing[^1].Position + 1;
            foreach (var trackId in trackIds)
            {
                _context.QueueItems.Add(new QueueItem
                {
                    UserId = userId,
                    TrackId = trackId,
                    Position = position++,
                    EnqueuedAt = DateTime.UtcNow,
                });
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return [];
    }

    public async Task<bool> RemoveItem(UserId userId, long itemId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var item = await _context.QueueItems
            .FirstOrDefaultAsync(q => q.UserId == userId && q.Id == itemId);
        if (item == null)
        {
            return false;
        }

        _context.QueueItems.Remove(item);

        var survivors = await _context.QueueItems
            .Where(q => q.UserId == userId && q.Id != itemId)
            .OrderBy(q => q.Position)
            .ToListAsync();
        for (var n = 0; n < survivors.Count; n++)
        {
            survivors[n].Position = n + 1;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public Task Clear(UserId userId)
    {
        return _context.QueueItems.Where(q => q.UserId == userId).ExecuteDeleteAsync();
    }

    private async Task<List<TrackId>> UnknownTracks(List<TrackId> trackIds)
    {
        var requested = trackIds.Distinct().ToList();
        var known = await _context.Tracks
            .Where(t => requested.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();
        return requested.Except(known).ToList();
    }
}
