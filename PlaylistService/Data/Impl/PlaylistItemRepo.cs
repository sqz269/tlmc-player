using Microsoft.EntityFrameworkCore;
using PlaylistService.Data.Api;
using PlaylistService.Model;

namespace PlaylistService.Data.Impl;

public class PlaylistItemRepo : IPlaylistItemRepo
{
    private readonly AppDbContext _context;

    public PlaylistItemRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> IncPlaylistItem(Guid playlist, Guid trackId)
    {
        int newVal = await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlist && pi.TrackId == trackId)
            .ExecuteUpdateAsync(pi => pi.SetProperty(
                p => p.TimesPlayed, f => f.TimesPlayed + 1));

        return newVal;
    }

    public async Task<bool> DoesPlaylistItemExist(Guid playlist, Guid trackId)
    {
        return await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlist && pi.TrackId == trackId)
            .AnyAsync();
    }

    public async Task<PlaylistItem> InsertPlaylistItem(Guid playlistId, Guid trackId)
    {
        var playlist = _context.Playlists
            .Where(p => p.Id == playlistId)
            .Include(p => p.Tracks)
            .FirstOrDefault();

        if (playlist == null)
        {
            throw new NullReferenceException($"Playlist ({playlistId}) Does not exist");
        }

        var playlistItem = new PlaylistItem
        {
            TrackId = trackId,
            Playlist = playlist,
            DateAdded = DateTime.UtcNow,
            Index = playlist.NumberOfTracks + 1,
            TimesPlayed = 0
        };

        playlist.LastModified = DateTime.UtcNow;

        playlist.Tracks.Add(playlistItem);
        playlist.NumberOfTracks += 1;

        var addedItem = await _context.PlaylistItems.AddAsync(playlistItem);

        await _context.SaveChangesAsync();

        return addedItem.Entity;
    }

    public async Task<PlaylistItem> DeletePlaylistItem(Guid playlistId, Guid trackId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var item = await _context.PlaylistItems
            .Where(pi => pi.TrackId == trackId && pi.PlaylistId == playlistId)
            .FirstOrDefaultAsync();

        try
        {
            var updated = await _context.PlaylistItems
                .Where(pi => pi.PlaylistId == playlistId && pi.Index > item.Index)
                .ExecuteUpdateAsync(e =>
                    e.SetProperty(p => p.Index, ind => ind.Index - 1));
            Console.WriteLine($"Updated: {updated} Records");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        //await _context.Playlists
        //    .Where(p => p.Id == playlistId)
        //    .ExecuteUpdateAsync(p =>
        //        p.SetProperty(p => p.LastModified, p => DateTime.UtcNow));

        var playlist = await _context.Playlists.Where(p => p.Id == playlistId).FirstOrDefaultAsync();
        playlist.LastModified = DateTime.UtcNow;
        playlist.NumberOfTracks--;

        _context.PlaylistItems.Remove(item);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return item;
    }

    public async Task<List<PlaylistItem>> InsertPlaylistItems(Guid playlist, List<Guid> trackIds)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var playlistItems = new List<PlaylistItem>();

        var playlistEntity = await _context.Playlists
            .Where(p => p.Id == playlist)
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync();

        if (playlistEntity == null)
        {
            throw new NullReferenceException($"Playlist ({playlist}) Does not exist");
        }

        foreach (var trackId in trackIds)
        {
            var playlistItem = new PlaylistItem
            {
                TrackId = trackId,
                Playlist = playlistEntity,
                DateAdded = DateTime.UtcNow,
                Index = playlistEntity.NumberOfTracks + 1,
                TimesPlayed = 0
            };

            playlistEntity.Tracks.Add(playlistItem);
            playlistEntity.NumberOfTracks += 1;

            playlistItems.Add(playlistItem);
        }

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

        try
        {
            var max = items.Max(i => i.Index);

            var updated = await _context.PlaylistItems
                .Where(pi => pi.PlaylistId == playlistId && pi.Index > max)
                .ExecuteUpdateAsync(e =>
                    e.SetProperty(p => p.Index, ind => ind.Index - items.Count));
            Console.WriteLine($"Updated: {updated} Records");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        var playlist = await _context.Playlists.Where(p => p.Id == playlistId).FirstOrDefaultAsync();
        playlist.LastModified = DateTime.UtcNow;
        playlist.NumberOfTracks -= items.Count;

        _context.PlaylistItems.RemoveRange(items);

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