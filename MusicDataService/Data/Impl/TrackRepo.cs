using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class TrackRepo : ITrackRepo
{
    private readonly AppDbContext _context;

    public TrackRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<Track?> GetTrack(Guid trackId)
    {
        var track = await _context.Tracks.Where(t => t.Id == trackId)
            .Include(t => t.Original)
            .ThenInclude(og => og.Album)
            .Include(t => t.Album)
            .ThenInclude(a => a.Thumbnail)
            .Include(t => t.Album)
            .ThenInclude(a => a.AlbumArtist)
            .Include(t => t.TrackFile)
            .FirstOrDefaultAsync();

        //if (track != null)
        //{
        //    track.Album.Tracks = null;
        //}
        return track;
    }

    public async Task<Guid> CreateTrack(Guid albumId, Track track)
    {
        if (track.Id == Guid.Empty)
        {
            track.Id = Guid.NewGuid();
        }

        var album = await _context.Albums.Where(a => a.Id == albumId).FirstOrDefaultAsync();
        if (album == null)
        {
            throw new ArgumentException($"No album found with given Album Id: {albumId}", nameof(albumId));
        }

        track.Album = album;
        album.Tracks.Add(track);
        var addedTrack = await _context.Tracks.AddAsync(track);
        return addedTrack.Entity.Id;
    }

    public async Task<bool> UpdateTrack(Guid trackId, Track track)
    {
        throw new NotImplementedException();
    }
}