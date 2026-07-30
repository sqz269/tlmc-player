using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Precomputed circle-level similarity: symmetric chamfer between the circles'
/// release-centroid sets. Collab releases contribute their centroid to every
/// linked circle, so joint works inform both parents. Same three-flavor union
/// shape as <see cref="SimilarRelease"/>.
/// </summary>
public class SimilarCircle
{
    public CircleId AnchorCircleId { get; set; }
    public Circle AnchorCircle { get; set; } = null!;

    public CircleId NeighborCircleId { get; set; }
    public Circle NeighborCircle { get; set; } = null!;

    /// <summary>1-based rank in the style ordering; null outside its top list.</summary>
    public short? RankStyle { get; set; }

    /// <summary>1-based rank in the raw ordering; null outside its top list.</summary>
    public short? RankRaw { get; set; }

    /// <summary>1-based rank in the kde ordering; null outside its top list.</summary>
    public short? RankKde { get; set; }

    /// <summary>Duplicate-suppressed chamfer; see <see cref="SimilarRelease.ScoreStyle"/>.</summary>
    public float ScoreStyle { get; set; }

    /// <summary>Plain chamfer; >= ~0.999 means shared recordings somewhere in both catalogues.</summary>
    public float ScoreRaw { get; set; }

    /// <summary>Kernel mean embedding cosine; the display-friendly scale.</summary>
    public float ScoreKde { get; set; }
}
