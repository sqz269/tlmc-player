using TlmcPlayerBackend.Dtos.MusicData.Track;

namespace TlmcPlayerBackend.Models.Api;

public class TrackListResult
{
    public IEnumerable<TrackReadDto> Tracks { get; set; }
    public int Count { get; set; }
    public long Total { get; set; }
}