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

        await _context.PlaylistItems
            .Where(pi => pi.PlaylistId == playlistId && pi.Index > item.Index)
            .ExecuteUpdateAsync(e => 
                e.SetProperty(p => p.Index, ind => ind.Index - 1));

        await _context.Playlists
            .Where(p => p.Id == playlistId)
            .ExecuteUpdateAsync(p =>
                p.SetProperty(p => p.LastModified, p => DateTime.UtcNow));

        _context.PlaylistItems.Remove(item);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return item;
    }

    public async Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item)
    {
        throw new NotImplementedException();
    }
}