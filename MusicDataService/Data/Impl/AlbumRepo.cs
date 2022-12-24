using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class AlbumRepo : IAlbumRepo
{
    private readonly AppDbContext _context;

    public AlbumRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<Album>> GetAlbums(int start, int limit)
    {
        var albums = await _context.Albums.OrderBy(a => a.Id)
            .Skip(start).Take(limit)
            .Include(a => a.Tracks)
            .Include(a => a.AlbumImage)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .ToListAsync();

        // Avoid circular reference when serializing
        //albums.ForEach(album => album.Tracks.ForEach(track =>
        //{
        //    track.Album = default;
        //}));

        return albums;
    }

    public async Task<Album?> GetAlbum(Guid id)
    {
        var album = await _context.Albums.Where(a => a.Id == id)
            .Include(a => a.Tracks)!
            .ThenInclude(t => t.Original)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.TrackFile)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .FirstOrDefaultAsync();

        // Avoid circular reference when serializing
        //if (album != null)
        //{
        //    album.Tracks.ForEach(track => track.Album = null);
        //    return album;
        //}
        return album;
    }

    public Task UpdateAlbumData(Guid id, Album album)
    {
        throw new NotImplementedException();
    }

    public async Task<Guid> AddAlbum(Album album)
    {
        if (album.Id == Guid.Empty)
        {
            album.Id = Guid.NewGuid();
        }

        var addedAlbum = await _context.Albums.AddAsync(album);

        return addedAlbum.Entity.Id;
    }

    public async Task<Track> AddTrackToAlbum(Guid albumId, Track track)
    {
        var album = await GetAlbum(albumId);
        if (album == null)
            throw new ArgumentNullException(nameof(albumId), "No such album with Id");

        if (track.Id == Guid.Empty)
        {
            track.Id = new Guid();
        }
        
        track.Album = album;

        var addedTrack = await _context.Tracks.AddAsync(track);
        album.Tracks.Add(addedTrack.Entity);

        return addedTrack.Entity;
    }

    public Task<Track> GetTrack(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task UpdateTrackData(Track track)
    {
        throw new NotImplementedException();
    }
}