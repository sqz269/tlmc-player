namespace TlmcPlayerBackend.Models.Api;

[Flags]
public enum TrackSimilarityRankingPenaltyAttribute
{
    None = 0,
    Title = 1 << 0,
    Artist = 1 << 1,
    Album = 1 << 2,
    CosineDistThreshold = 1 << 3
}
