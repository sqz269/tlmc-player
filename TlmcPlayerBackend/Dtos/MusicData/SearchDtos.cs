using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Dtos.MusicData;

/// <summary>
/// One search result: the same track-with-context shape every list endpoint
/// returns, hydrated from Postgres, plus the highlight fragments — which only
/// exist in the engine, so they ride along (SCHEMA-V6.md section 15).
/// </summary>
public class SearchHitDto
{
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public List<CircleSlimDto> Circles { get; set; } = [];
    public ArtworkId? ArtworkId { get; set; }

    /// <summary>Search-document field → fragment(s) with `&lt;em&gt;` marks, only
    /// for fields the query matched. Values are a string or an array of strings.</summary>
    public Dictionary<string, object> Highlights { get; set; } = [];
}

public class SearchResponseDto
{
    public string Query { get; set; } = null!;
    public List<SearchHitDto> Items { get; set; } = [];

    /// <summary>Estimated, not exact — the engine's tradeoff, mirrored honestly.</summary>
    public long EstimatedTotalHits { get; set; }

    public int Limit { get; set; }
    public int Offset { get; set; }
    public long ProcessingTimeMs { get; set; }
}
