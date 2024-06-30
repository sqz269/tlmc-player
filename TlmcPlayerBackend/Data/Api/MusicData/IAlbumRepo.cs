using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IAlbumRepo
{
    public Task<bool> SaveChanges();

    public Task<long> CountTotalAlbums();

    public Task<Tuple<IEnumerable<Album>, long>> GetAlbums(int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder);

    public Task<IEnumerable<Album>> GetAlbumsFiltered(AlbumFilter filter, int start, int limit);

    public Task<Album?> GetAlbum(Guid id);

    public Task<Tuple<IEnumerable<Album>, IEnumerable<Guid>>> GetAlbums(IList<Guid> albumIds);

    public Task UpdateAlbumData(Guid id, Album album);

    public Task<Guid> AddAlbum(Album album);

    public Task<Track> AddTrackToAlbum(Guid albumId, Track track);
}