using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class OriginalAlbumRepo : IOriginalAlbumRepo
{
    private readonly AppDbContext _context;

    public OriginalAlbumRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<OriginalAlbum>> GetOriginalAlbums(int start, int limit)
    {
        return await _context.OriginalAlbums.OrderBy(a => a.Id).Skip(start).Take(limit).ToListAsync();
    }

    public async Task<OriginalAlbum?> GetOriginalAlbum(string id)
    {
        return await _context.OriginalAlbums.Where(a => a.Id == id).FirstOrDefaultAsync();
    }

    public async Task<string> AddOriginalAlbum(OriginalAlbum originalAlbum)
    {
        if (originalAlbum.Id == null)
        {
            throw new ArgumentNullException(nameof(originalAlbum.Id), "An Id for [OriginalAlbum] must be provided when adding an album");
        }

        var orgAlbum = await _context.OriginalAlbums.AddAsync(originalAlbum);
        return orgAlbum.Entity.Id;
    }

    public async Task<OriginalTrack> AddOriginalTrackToAlbum(string albumId, OriginalTrack originalTrack)
    {
        if (originalTrack.Id == null)
        {
            throw new ArgumentNullException(nameof(originalTrack.Id), "An Id for [OriginalTrack] must be provided when adding a track");
        }

        var album = await _context.OriginalAlbums.Where(a => a.Id == albumId).FirstOrDefaultAsync();
        if (album == null)
        {
            throw new ArgumentException($"No Original Album found with given Original Album Id: {albumId}", nameof(albumId));
        }

        originalTrack.Album = album;
        album.Tracks.Add(originalTrack);
        var addedTrack = await _context.OriginalTracks.AddAsync(originalTrack);
        return addedTrack.Entity;
    }
}