using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface ICircleRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<Circle>> GetCircles(int start, int limit);

    public Task<IEnumerable<Circle>> GetCircles(IEnumerable<Guid> ids);

    public Task<Tuple<IEnumerable<Album>, long>?> GetCircleAlbums(Guid id, int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder);

    public Task<Tuple<IEnumerable<Album>, long>?> GetCircleAlbums(string name, int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder);

    public Task<Circle?> GetCircleByName(string name);

    public Task<Circle?> GetCircleById(Guid id);

    public Task<Guid> AddCircle(Circle circle);

    public Task<string> AddCircleWebsite(Guid circleId, CircleWebsite website);
}