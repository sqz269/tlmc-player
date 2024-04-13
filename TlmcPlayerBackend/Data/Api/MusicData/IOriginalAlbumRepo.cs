using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IOriginalAlbumRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<OriginalAlbum>> GetOriginalAlbums(int start, int limit);

    public Task<OriginalAlbum?> GetOriginalAlbum(string id);

    public Task<string> AddOriginalAlbum(OriginalAlbum originalAlbum);

    public Task<OriginalTrack> AddOriginalTrackToAlbum(string albumId, OriginalTrack originalTrack);
}