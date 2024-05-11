using TlmcPlayerBackend.Controllers.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface ITrackRepo
{
    public Task<bool> SaveChanges();

    public Task<Track?> GetTrack(Guid trackId);

    public Task<Tuple<List<Track>, List<Guid>>> GetTracks(IList<Guid> tracks);

    public Task<IEnumerable<Track>> SampleRandomTrack(int limit, TrackFilterSelectableRanged? filters);

    public Task<Guid> CreateTrack(Guid albumGuid, Track track);

    public Task<bool> UpdateTrack(Guid trackId, Track track);
}