namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Human-reviewed display name for one acoustic-family label on the embedding map
/// (proposed by the MuQ-MuLan naming stage, curated, loaded by
/// apply_cluster_names.sql). Wipe-and-reload with the layout: labels are
/// relabeled by size every layout run, so names never outlive the coordinates
/// they were listened against.
/// </summary>
public class TrackMapCluster
{
    public short Cluster { get; set; }

    public string Name { get; set; } = null!;
}
