using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

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
        return await _context.Albums.OrderBy(a => a.Id).Skip(start).Take(limit).ToListAsync();
    }

    public async Task<Album?> GetAlbum(Guid id)
    {
        return await _context.Albums.Where(a => a.Id == id).FirstOrDefaultAsync();
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