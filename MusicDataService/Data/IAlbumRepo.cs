using MusicDataService.Models;

namespace MusicDataService.Data;

public interface IAlbumRepo
{
    public Album GetAlbum(Guid id);

    public void UpdateAlbumData(Guid id, Album album);

    public Guid AddAlbum(Album album);

    public void AddTrackToAlbum(Guid albumId, Track track);

    public Track GetTrack(Guid id);

    public void UpdateTrackData(Track track);
}