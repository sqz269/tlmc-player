using MusicDataService.Models;

namespace MusicDataService.Data.Api;

public interface IOriginalTrackRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<OriginalTrack>> GetOriginalTracks(int start, int limit);

    public Task<OriginalTrack?> GetOriginalTrack(string trackId);

    public Task<IEnumerable<OriginalTrack>> GetOriginalTracks(IEnumerable<string> trackIds);
}