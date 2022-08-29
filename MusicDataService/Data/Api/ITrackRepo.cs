using MusicDataService.Models;

namespace MusicDataService.Data.Api;

public interface ITrackRepo
{
    public Task<bool> SaveChanges();

    public Task<Track?> GetTrack(Guid trackId);

    public Task<Guid> CreateTrack(Guid albumGuid, Track track);

    public Task<bool> UpdateTrack(Guid trackId, Track track);
}