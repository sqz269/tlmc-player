using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Precomputed release-level similarity: symmetric chamfer between the releases'
/// track-vector sets, three flavors per neighbour. Rows are the union of each
/// flavor's top list, so the rank columns are nullable and every row carries all
/// three scores.
/// </summary>
public class SimilarRelease
{
    public ReleaseId AnchorReleaseId { get; set; }
    public Release AnchorRelease { get; set; } = null!;

    public ReleaseId NeighborReleaseId { get; set; }
    public Release NeighborRelease { get; set; } = null!;

    /// <summary>1-based rank in the style ordering; null outside its top list.</summary>
    public short? RankStyle { get; set; }

    /// <summary>1-based rank in the raw ordering; null outside its top list.</summary>
    public short? RankRaw { get; set; }

    /// <summary>1-based rank in the kde ordering; null outside its top list.</summary>
    public short? RankKde { get; set; }

    /// <summary>
    /// Chamfer with shared recordings suppressed: duplicate track pairs are removed
    /// from the match, so a re-release scores near 0 here instead of topping the
    /// list. Compressed near the top like the track score — display should rescale.
    /// </summary>
    public float ScoreStyle { get; set; }

    /// <summary>
    /// Plain chamfer. >= ~0.999 means the releases share recordings (re-release,
    /// compilation, duplicate rip) — a versions signal, not a similarity one.
    /// </summary>
    public float ScoreRaw { get; set; }

    /// <summary>
    /// Cosine of RBF kernel mean embeddings (the KDE overlap integral). Weighs where
    /// the distribution's mass sits, and spans a genuinely usable 0..1 scale.
    /// </summary>
    public float ScoreKde { get; set; }
}
