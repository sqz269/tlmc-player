using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IOriginalTrackRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<OriginalTrack>> GetOriginalTracks(int start, int limit);

    public Task<OriginalTrack?> GetOriginalTrack(string trackId);

    public Task<IEnumerable<OriginalTrack>> GetOriginalTracks(IEnumerable<string> trackIds);
}