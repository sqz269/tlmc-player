using TlmcPlayerBackend.Dtos.MusicData.Track;

namespace TlmcPlayerBackend.Models.Api;

public class TrackRandomResult
{
    public IEnumerable<TrackReadDto> Tracks { get; set; }
    public int Count { get; set; }
    public long Total { get; set; }
    public string Seed { get; set; }
}