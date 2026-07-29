using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data.Api.Playlist;

namespace TlmcPlayerBackend.Data.Impl.Playlist;
using TlmcPlayerBackend.Models.Playlist;

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

    public async Task<bool> DoesPersonalPlaylistExist(Guid userId)
    {
        return await _context.Playlists.AnyAsync(x => x.OwnerId == userId && x.Type == PlaylistType.Favorite);
    }

    public async Task<IEnumerable<Playlist>> GetUserPlaylist(Guid ownerId, Guid? userId)
    {
        if (ownerId == userId)
        {
            return await _context.Playlists
                .Where(p => p.OwnerId == ownerId)
                .ToListAsync();
        }

        return await _context.Playlists
            .Where(p =>
                p.OwnerId == ownerId &&
                p.Visibility == PlaylistVisibility.Public)
            .ToListAsync();
    }

    public async Task<Playlist?> GetPlaylist(Guid playlistId, bool noTracking=false)
    {
        var query = _context.Playlists.Where(p => p.Id == playlistId);
        if (noTracking)
        {
            // LINQ operators return a new query; the previous code discarded this
            // result, so noTracking:true still handed back tracked entities.
            query = query.AsNoTracking();
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
                    (p.OwnerId == userId ||
                     p.Visibility == PlaylistVisibility.Public ||
                     p.Visibility == PlaylistVisibility.Unlisted))
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetOwnedPlaylist(Guid playlistId, Guid ownerId)
    {
        return await _context.Playlists
            .Where(p => p.Id == playlistId && p.OwnerId == ownerId)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetHistoryPlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.OwnerId == userId && p.Type == PlaylistType.History)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetFavoritesPlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.OwnerId == userId && p.Type == PlaylistType.Favorite)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> GetQueuePlaylist(Guid userId)
    {
        return await _context.Playlists
            .Where(p => p.OwnerId == userId && p.Type == PlaylistType.Queue)
            .FirstOrDefaultAsync();
    }

    public async Task<Playlist?> InsertPlaylist(Playlist playlist)
    {
        var entry = await _context.Playlists.AddAsync(playlist);
        var saved = await SaveChanges();

        return saved ? entry.Entity : null;
    }

    public async Task<bool> InsertPlaylists(IEnumerable<Playlist> playlists)
    {
        foreach (var playlist in playlists)
        {
            await _context.Playlists.AddAsync(playlist);
        }

        var saved = await SaveChanges();
        return saved;
    }

    public async Task<bool> DeletePlaylist(Guid playlistId, Guid ownerId)
    {
        var playlist = await _context.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerId == ownerId);

        if (playlist == null)
        {
            return false;
        }

        _context.Playlists.Remove(playlist);
        return await SaveChanges();
    }
}