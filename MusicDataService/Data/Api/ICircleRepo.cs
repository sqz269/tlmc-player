using MusicDataService.Dtos.Album;
using MusicDataService.Models;

namespace MusicDataService.Data.Api;

public interface ICircleRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<Circle>> GetCircles(int start, int limit);

    public Task<IEnumerable<Circle>> GetCircles(IEnumerable<Guid> ids);

    public Task<IEnumerable<Album>?> GetCircleAlbums(Guid id, int start, int limit);

    public Task<IEnumerable<Album>?> GetCircleAlbums(string name, int start, int limit);

    public Task<Circle?> GetCircleByName(string name);

    public Task<Circle?> GetCircleById(Guid id);

    public Task<Guid> AddCircle(Circle circle);

    public Task<string> AddCircleWebsite(Guid circleId, CircleWebsite website);
}