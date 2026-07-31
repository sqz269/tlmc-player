using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Dtos.MusicData;

/// <summary>
/// One personalized home row (Docs/RECOMMENDER.md section 4). Only ids travel:
/// clients hydrate tracks through whichever surface they already speak, and
/// the server logs a rec_impression per id at serve time.
/// </summary>
public class HomeRowDto
{
    /// <summary>Canonical surface tag, e.g. "home.because" — the registry lives in RECOMMENDER.md.</summary>
    public string Surface { get; set; } = null!;

    /// <summary>Display context: the anchor track's name, the family name, …</summary>
    public string? Title { get; set; }

    public List<TrackId> TrackIds { get; set; } = [];
}

public class RecommendationHomeDto
{
    public List<HomeRowDto> Rows { get; set; } = [];
}
