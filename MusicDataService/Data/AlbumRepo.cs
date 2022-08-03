using MusicDataService.Models;

namespace MusicDataService.Data;

public class AlbumRepo : IAlbumRepo
{
    public AlbumRepo()
    {
    }

    public Album GetAlbum(Guid id)
    {
        throw new NotImplementedException();
    }

    public void UpdateAlbumData(Guid id, Album album)
    {
        throw new NotImplementedException();
    }

    public Guid AddAlbum(Album album)
    {
        throw new NotImplementedException();
    }

    public void AddTrackToAlbum(Guid albumId, Track track)
    {
        throw new NotImplementedException();
    }

    public Track GetTrack(Guid id)
    {
        throw new NotImplementedException();
    }

    public void UpdateTrackData(Track track)
    {
        throw new NotImplementedException();
    }
}