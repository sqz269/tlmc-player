using Microsoft.EntityFrameworkCore;
using PlaylistService.Data.Api;
using PlaylistService.Model;

namespace PlaylistService.Data.Impl;

public class PlaylistRepo : IPlaylistRepo
{
    private readonly AppDbContext _context;

    public PlaylistRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<Playlist>> GetUserPlaylist(Guid ownerId, Guid? userId)
    {
        if (ownerId == userId)
        {
            return await _context.Playlists
                .Where(p => p.UserId == ownerId)
                .ToListAsync();
        }

        return await _context.Playlists
            .Where(p =>
                p.UserId == ownerId &&
                p.Visibility == PlaylistVisibility.Public)
            .ToListAsync();
    }

    public async Task<Playlist?> GetPlaylist(Guid playlistId, bool noTracking=false)
    {
        var query = _context.Playlists.Where(p => p.Id == playlistId);
        if (noTracking)
        {
            query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetPlaylist(Guid playlistId, Guid? userId)
    {
        if (userId == null)
        {
            return await _context.Playlists
                .Where(p =>
                    p.Id == playlistId &&
                    (p.Visibility == PlaylistVisibility.Public ||
                     p.Visibility == PlaylistVisibility.Unlisted))
                .FirstOrDefaultAsync();
        }

        return await _context.Playlists
            .Where(p => 
                    p.Id == playlistId && 
                    (p.UserId == userId || 
                     p.Visibility == PlaylistVisibility.Public || 
                     p.Visibility == PlaylistVisibility.Unlisted))
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetHistoryPlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.UserId == userId && p.Type == PlaylistType.History)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetFavoritesPlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.UserId == userId && p.Type == PlaylistType.Favorite)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetQueuePlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.UserId == userId && p.Type == PlaylistType.Queue)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> InsertPlaylist(Playlist playlist)
    {
        var entry = await _context.Playlists.AddAsync(playlist);
        var saved = await SaveChanges();

        return saved ? entry.Entity : null;
    }
}