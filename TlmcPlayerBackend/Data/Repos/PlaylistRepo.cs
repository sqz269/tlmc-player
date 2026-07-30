using Microsoft.EntityFrameworkCore;
using Npgsql;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Data.Repos;

public record PlaylistMeta(PlaylistId Id, UserId OwnerId, PlaylistVisibility Visibility, PlaylistKind Kind);

public interface IPlaylistRepo
{
    Task<List<PlaylistReadDto>> GetUserPlaylists(UserId ownerId);
    Task<PlaylistMeta?> GetPlaylistMeta(PlaylistId id);
    Task<PlaylistReadDto?> GetPlaylist(PlaylistId id);
    Task<PlaylistReadDto> CreatePlaylist(UserId ownerId, string name, PlaylistVisibility visibility);
    Task<PlaylistReadDto?> UpdatePlaylist(PlaylistId id, string name, PlaylistVisibility visibility);
    Task<bool> DeletePlaylist(PlaylistId id);
    Task<PlaylistReadDto> GetOrCreateFavorite(UserId ownerId);

    Task<List<PlaylistItemDto>> GetItems(PlaylistId id, int start, int limit);

    /// <summary>Adds tracks; unknown ids are returned (callers answer 400), already
    /// present ids are silently skipped, order of the request is preserved.</summary>
    Task<List<TrackId>> AddItems(PlaylistId id, List<TrackId> trackIds);

    Task<int> RemoveItems(PlaylistId id, List<TrackId> trackIds);
    Task<bool> MoveItem(PlaylistId id, TrackId trackId, int toPosition);
}

public class PlaylistRepo(AppDbContext context) : IPlaylistRepo
{
    private readonly AppDbContext _context = context;

    public Task<List<PlaylistReadDto>> GetUserPlaylists(UserId ownerId)
    {
        return ProjectPlaylists(_context.Playlists.AsNoTracking().Where(p => p.OwnerId == ownerId))
            .OrderByDescending(p => p.LastModified)
            .ToListAsync();
    }

    public async Task<PlaylistMeta?> GetPlaylistMeta(PlaylistId id)
    {
        var meta = await _context.Playlists
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.OwnerId, p.Visibility, p.Kind })
            .FirstOrDefaultAsync();

        return meta == null ? null : new PlaylistMeta(meta.Id, meta.OwnerId, meta.Visibility, meta.Kind);
    }

    public Task<PlaylistReadDto?> GetPlaylist(PlaylistId id)
    {
        return ProjectPlaylists(_context.Playlists.AsNoTracking().Where(p => p.Id == id))
            .FirstOrDefaultAsync()!;
    }

    public async Task<PlaylistReadDto> CreatePlaylist(UserId ownerId, string name, PlaylistVisibility visibility)
    {
        var playlist = Playlist.Create(name, visibility, ownerId);
        playlist.CreatedAt = DateTime.UtcNow;
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return (await GetPlaylist(playlist.Id))!;
    }

    public async Task<PlaylistReadDto?> UpdatePlaylist(PlaylistId id, string name, PlaylistVisibility visibility)
    {
        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == id);
        if (playlist == null)
        {
            return null;
        }

        playlist.Name = name;
        playlist.Visibility = visibility;
        playlist.LastModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await GetPlaylist(id);
    }

    public async Task<bool> DeletePlaylist(PlaylistId id)
    {
        var deleted = await _context.Playlists.Where(p => p.Id == id).ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<PlaylistReadDto> GetOrCreateFavorite(UserId ownerId)
    {
        var existing = await ProjectPlaylists(_context.Playlists.AsNoTracking()
                .Where(p => p.OwnerId == ownerId && p.Kind == PlaylistKind.Favorite))
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            return existing;
        }

        try
        {
            var playlist = Playlist.Create("Favorites", PlaylistVisibility.Private, ownerId, PlaylistKind.Favorite);
            playlist.CreatedAt = DateTime.UtcNow;
            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();
            return (await GetPlaylist(playlist.Id))!;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost the bootstrap race to a concurrent request; the partial unique
            // index playlist_one_favorite_per_owner guarantees the winner exists.
            _context.ChangeTracker.Clear();
            return (await ProjectPlaylists(_context.Playlists.AsNoTracking()
                    .Where(p => p.OwnerId == ownerId && p.Kind == PlaylistKind.Favorite))
                .FirstAsync());
        }
    }

    public Task<List<PlaylistItemDto>> GetItems(PlaylistId id, int start, int limit)
    {
        return _context.PlaylistItems
            .AsNoTracking()
            .Where(i => i.PlaylistId == id)
            .OrderBy(i => i.Position)
            .Skip(start)
            .Take(limit)
            .Select(i => new PlaylistItemDto
            {
                Position = i.Position,
                AddedAt = i.AddedAt,
                Track = new Dtos.MusicData.TrackListItemDto
                {
                    Id = i.Track.Id,
                    TrackNumber = i.Track.TrackNumber,
                    Name = i.Track.Name,
                    Duration = i.Track.Duration,
                    HasMedia = i.Track.MediaKey != null,
                    HasLyrics = i.Track.LyricsId != null,
                },
                Release = new Dtos.MusicData.ReleaseSlimDto
                {
                    Id = i.Track.Disc.Release.Id,
                    Name = i.Track.Disc.Release.Name,
                },
                ArtworkId = i.Track.Disc.Release.ArtworkId,
            })
            .ToListAsync();
    }

    public async Task<List<TrackId>> AddItems(PlaylistId id, List<TrackId> trackIds)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var playlist = await _context.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Playlist {id} does not exist");

        var requested = trackIds.Distinct().ToList();
        var known = await _context.Tracks
            .Where(t => requested.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();
        var unknown = requested.Except(known).ToList();
        if (unknown.Count > 0)
        {
            return unknown;
        }

        var present = playlist.Items.Select(i => i.TrackId).ToHashSet();
        // Derived from the rows, never from a stored counter (the v5 one drifted).
        var nextPosition = playlist.Items.Count == 0 ? 1 : playlist.Items.Max(i => i.Position) + 1;

        foreach (var trackId in requested.Where(t => !present.Contains(t)))
        {
            playlist.Items.Add(new PlaylistItem
            {
                PlaylistId = id,
                TrackId = trackId,
                Position = nextPosition++,
                AddedAt = DateTime.UtcNow,
            });
        }

        playlist.LastModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return [];
    }

    public async Task<int> RemoveItems(PlaylistId id, List<TrackId> trackIds)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var items = await _context.PlaylistItems
            .Where(i => i.PlaylistId == id && trackIds.Contains(i.TrackId))
            .ToListAsync();

        // A retried or duplicated delete matches nothing; that is a no-op, not a 500.
        if (items.Count == 0)
        {
            await transaction.CommitAsync();
            return 0;
        }

        _context.PlaylistItems.RemoveRange(items);

        // Compact the survivors to a dense 1..N. The position unique is DEFERRABLE,
        // so the whole shuffle commits as one consistent state.
        var survivors = await _context.PlaylistItems
            .Where(i => i.PlaylistId == id && !trackIds.Contains(i.TrackId))
            .OrderBy(i => i.Position)
            .ToListAsync();

        for (var n = 0; n < survivors.Count; n++)
        {
            survivors[n].Position = n + 1;
        }

        var playlist = await _context.Playlists.FirstAsync(p => p.Id == id);
        playlist.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return items.Count;
    }

    public async Task<bool> MoveItem(PlaylistId id, TrackId trackId, int toPosition)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var items = await _context.PlaylistItems
            .Where(i => i.PlaylistId == id)
            .OrderBy(i => i.Position)
            .ToListAsync();

        var moving = items.FirstOrDefault(i => i.TrackId == trackId);
        if (moving == null)
        {
            return false;
        }

        items.Remove(moving);
        var target = Math.Clamp(toPosition, 1, items.Count + 1);
        items.Insert(target - 1, moving);

        for (var n = 0; n < items.Count; n++)
        {
            items[n].Position = n + 1;
        }

        var playlist = await _context.Playlists.FirstAsync(p => p.Id == id);
        playlist.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private static IQueryable<PlaylistReadDto> ProjectPlaylists(IQueryable<Playlist> query)
    {
        return query.Select(p => new PlaylistReadDto
        {
            Id = p.Id,
            Name = p.Name,
            Kind = p.Kind,
            Visibility = p.Visibility,
            OwnerId = p.OwnerId,
            OwnerDisplayName = p.Owner.DisplayName,
            CreatedAt = p.CreatedAt,
            LastModified = p.LastModified,
            TrackCount = p.Items.Count,
        });
    }
}
