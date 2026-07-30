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

public class SimilarReleaseItemDto
{
    public ReleaseSlimDto Release { get; set; } = null!;
    public Ids.ArtworkId? ArtworkId { get; set; }
    public List<CircleSlimDto> Circles { get; set; } = [];

    /// <summary>Duplicate-suppressed chamfer. Compressed near the top; display Relevance.</summary>
    public float ScoreStyle { get; set; }

    /// <summary>Plain chamfer; >= ~0.999 marks shared recordings (a versions signal).</summary>
    public float ScoreRaw { get; set; }

    /// <summary>Kernel mean embedding cosine; usable as a 0..1 display scale directly.</summary>
    public float ScoreKde { get; set; }

    /// <summary>Min-max of the ordering flavor's score within this response.</summary>
    public float Relevance { get; set; }
}

public class SimilarReleasesResponseDto
{
    public List<SimilarReleaseItemDto> Items { get; set; } = [];

    /// <summary>'style' or 'kde' — the precomputed ordering this list follows.</summary>
    public string Flavor { get; set; } = null!;

    /// <summary>From embedding_config — the basis that produced these neighbours.</summary>
    public string? Model { get; set; }
}

public class SimilarCircleItemDto
{
    public CircleSlimDto Circle { get; set; } = null!;
    public float ScoreStyle { get; set; }
    public float ScoreRaw { get; set; }
    public float ScoreKde { get; set; }
    public float Relevance { get; set; }
}

public class SimilarCirclesResponseDto
{
    public List<SimilarCircleItemDto> Items { get; set; } = [];
    public string Flavor { get; set; } = null!;
    public string? Model { get; set; }
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
