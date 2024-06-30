using TlmcPlayerBackend.Dtos.MusicData.Track;

namespace TlmcPlayerBackend.Models.Api;

public class TrackGetMultipleResp
{
    public IEnumerable<TrackReadDto> Tracks { get; set; }
    public IEnumerable<Guid> NotFound { get; set; }
}