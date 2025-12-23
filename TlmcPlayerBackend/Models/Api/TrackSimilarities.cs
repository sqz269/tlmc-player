using TlmcPlayerBackend.Dtos.MusicData.Track;

namespace TlmcPlayerBackend.Models.Api;

public class TrackSimilarity
{
    public required TrackReadDto Track { get; set; }
    public required double SimilarityScore { get; set; }
}

public class TrackSimilaritiesResult
{
    public required IEnumerable<TrackSimilarity> Items { get; set; }
    public required int Count { get; set; }
}