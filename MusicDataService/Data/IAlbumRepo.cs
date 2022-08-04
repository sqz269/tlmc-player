using MusicDataService.Models;

namespace MusicDataService.Data;

public interface IAlbumRepo
{
    public Task<bool> SaveChanges();

    public Task<Album?> GetAlbum(Guid id);

    public Task UpdateAlbumData(Guid id, Album album);

    public Task<Guid> AddAlbum(Album album);

    public Task AddTrackToAlbum(Guid albumId, Track track);

    public Task<Track> GetTrack(Guid id);

    public Task UpdateTrackData(Track track);
}