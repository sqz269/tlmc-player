using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class OriginalTrackRepo : IOriginalTrackRepo
{
    private readonly AppDbContext _context;

    public OriginalTrackRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<OriginalTrack?> GetOriginalTrack(string trackId)
    {
        return await _context.OriginalTracks.Where(t => t.Id == trackId).FirstOrDefaultAsync();
    }

    public async Task<string> AddOriginalTrack(string albumId, OriginalTrack originalTrack)
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
        return addedTrack.Entity.Id;
    }
}