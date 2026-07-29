using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data.Api.Playlist;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Data.Impl.Playlist;

public class PlaylistItemRepo(AppDbContext context) : IPlaylistItemRepo
{
    private readonly AppDbContext _context = context;

    public async Task<int> IncPlaylistItem(Guid playlist, Guid trackId)
    {
        int newVal = await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlist && pi.TrackId == trackId)
            .ExecuteUpdateAsync(pi => pi.SetProperty(
                p => p.TimesPlayed, f => f.TimesPlayed + 1));

        return newVal;
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="trackIds"></param>
    /// <returns>A list of track ids that exists in the playlist, If an id is not contained in the list but was passed in then that means the track does not exist in the list</returns>
    public Task<List<Guid>> DoesPlaylistItemsExistInPlaylist(Guid playlistId, List<Guid> trackIds)
    {
        return _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlistId && trackIds.Contains(pi.TrackId))
            .Select(e => e.TrackId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns the subset of <paramref name="trackIds"/> that do not exist as tracks.
    /// PlaylistItem.TrackId is a required foreign key, so inserting an unknown id
    /// fails inside the database and surfaces as a 500; callers use this to answer
    /// 400 instead.
    /// </summary>
    public async Task<List<Guid>> GetUnknownTrackIds(List<Guid> trackIds)
    {
        var known = await _context.Tracks
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(t => trackIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        return trackIds.Distinct().Except(known).ToList();
    }

    public async Task<PlaylistItem?> InsertPlaylistItem(Guid playlist, Guid trackId)
    {
        var inserted = await InsertPlaylistItems(playlist, [trackId]);
        return inserted.FirstOrDefault();
    }

    public async Task<PlaylistItem?> DeletePlaylistItem(Guid playlist, Guid trackId)
    {
        var removed = await DeletePlaylistItems(playlist, [trackId]);
        return removed.FirstOrDefault();
    }

    public async Task<List<PlaylistItem>> InsertPlaylistItems(Guid playlist, List<Guid> trackIds)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var playlistEntity = await _context.Playlists
            .Where(p => p.Id == playlist)
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync();

        if (playlistEntity == null)
        {
            throw new NullReferenceException($"Playlist ({playlist}) Does not exist");
        }

        // (TrackId, PlaylistId) is the primary key, so re-adding a track the playlist
        // already holds is a key violation rather than a no-op, and naming the same
        // track twice in one request collides the same way. Both are absorbed here.
        var alreadyPresent = playlistEntity.Tracks.Select(t => t.TrackId).ToHashSet();
        var toAdd = trackIds.Distinct().Where(id => !alreadyPresent.Contains(id)).ToList();

        var playlistItems = new List<PlaylistItem>();
        if (toAdd.Count == 0)
        {
            await transaction.CommitAsync();
            return playlistItems;
        }

        // Derived from the rows themselves rather than from NumberOfTracks, which is
        // a denormalised counter and can drift -- a cascade-deleted track removes a
        // PlaylistItem without ever decrementing it.
        var nextIndex = playlistEntity.Tracks.Count == 0
            ? 1
            : playlistEntity.Tracks.Max(t => t.Index) + 1;

        foreach (var trackId in toAdd)
        {
            var playlistItem = new PlaylistItem
            {
                TrackId = trackId,
                Playlist = playlistEntity,
                DateAdded = DateTime.UtcNow,
                Index = nextIndex++,
                TimesPlayed = 0
            };

            playlistEntity.Tracks.Add(playlistItem);
            playlistItems.Add(playlistItem);
        }

        playlistEntity.LastModified = DateTime.UtcNow;
        playlistEntity.NumberOfTracks = playlistEntity.Tracks.Count;

        await _context.PlaylistItems.AddRangeAsync(playlistItems);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return playlistItems;
    }

    public async Task<List<PlaylistItem>> DeletePlaylistItems(Guid playlistId, List<Guid> trackId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var items = await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlistId && trackId.Contains(pi.TrackId))
            .ToListAsync();

        // A retried or duplicated delete matches nothing. The previous code called
        // items.Max() unconditionally, which throws on an empty sequence, so an
        // ordinary double-click answered 500.
        if (items.Count == 0)
        {
            await transaction.CommitAsync();
            return items;
        }

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId);
        if (playlist == null)
        {
            await transaction.CommitAsync();
            return [];
        }

        _context.PlaylistItems.RemoveRange(items);
        await _context.SaveChangesAsync();

        // Compact what survives. Shifting only the rows above the highest deleted
        // index by the number of deletions -- the previous approach -- leaves rows
        // sitting between two deleted entries untouched, so deleting a
        // non-contiguous selection produced duplicate Index values that nothing
        // later ever repaired.
        var remaining = await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlistId)
            .OrderBy(pi => pi.Index)
            .ToListAsync();

        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].Index != i + 1)
            {
                remaining[i].Index = i + 1;
            }
        }

        playlist.LastModified = DateTime.UtcNow;
        // Assigned rather than decremented, so pre-existing drift is corrected here.
        playlist.NumberOfTracks = remaining.Count;

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return items;
    }

    public Task<List<PlaylistItem>> GetPlaylistItems(Guid playlist, int start, int limit)
    {
        var items = _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlist)
            .OrderBy(pi => pi.Index)
            .Skip(start)
            .Take(limit)
            .ToListAsync();

        return items;
    }

    public async Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item)
    {
        throw new NotImplementedException();
    }
}
