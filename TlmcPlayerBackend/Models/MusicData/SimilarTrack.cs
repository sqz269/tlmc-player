using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Precomputed chamfer neighbours — the primary similarity serving path. Loaded from
/// the precompute shard CSVs; the ANN tier over TrackEmbedding is the fallback for
/// tracks ingested since the last precompute.
/// </summary>
public class SimilarTrack
{
    public TrackId AnchorTrackId { get; set; }
    public Track AnchorTrack { get; set; } = null!;

    /// <summary>1-based neighbour rank.</summary>
    public short Rank { get; set; }

    public TrackId NeighborTrackId { get; set; }
    public Track NeighborTrack { get; set; } = null!;

    /// <summary>
    /// Symmetric chamfer similarity. The top of this distribution is compressed into
    /// roughly 0.986..0.994, and >= ~0.999 indicates a near-duplicate rip — the UI
    /// must rescale rather than show it raw.
    /// </summary>
    public float Score { get; set; }
}
