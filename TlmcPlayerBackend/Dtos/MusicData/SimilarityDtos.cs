namespace TlmcPlayerBackend.Dtos.MusicData;

public class SimilarTrackItemDto
{
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public List<CircleSlimDto> Circles { get; set; } = [];

    /// <summary>
    /// Raw chamfer similarity. The top of the distribution compresses into roughly
    /// 0.986..0.994 — display should use `relevance`, not this.
    /// </summary>
    public float Score { get; set; }

    /// <summary>Min-max normalized within this response; what a UI should show.</summary>
    public float Relevance { get; set; }
}

public class SimilarTracksResponseDto
{
    public List<SimilarTrackItemDto> Items { get; set; } = [];

    /// <summary>
    /// 'precomputed' (chamfer neighbour table) or 'approximate' (ANN over pooled
    /// vectors). Their quality is not the same; clients should not present the
    /// fallback as though it were.
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>From embedding_config — the basis that produced these neighbours.</summary>
    public string? Model { get; set; }
}
