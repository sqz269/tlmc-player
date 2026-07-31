using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// One point of the precomputed 2D embedding map: UMAP over TrackEmbedding, loaded
/// wipe-and-reload by the ETL (apply_track_map.sql) like similar_track — coordinates
/// from different layout runs are meaningless side by side. Year and work are
/// denormalized at load time so serving the whole map is a single-table scan.
/// </summary>
public class TrackMapPoint
{
    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    /// <summary>Layout coordinate, normalized into [-1, 1] with aspect ratio kept.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>Acoustic-family label (k-means over the embedding features, relabeled
    /// by descending size); -1 is reserved for unlabeled points.</summary>
    public short Cluster { get; set; }

    /// <summary>Release year through disc -> release; null when the release is undated.</summary>
    public short? Year { get; set; }

    /// <summary>First original work the track arranges; null for non-arrangements.</summary>
    public OriginalWorkId? WorkId { get; set; }

    public OriginalWork? Work { get; set; }

    /// <summary>Primary circle — lowest ordinal on the release; null when uncredited.</summary>
    public CircleId? CircleId { get; set; }

    public Circle? Circle { get; set; }
}
