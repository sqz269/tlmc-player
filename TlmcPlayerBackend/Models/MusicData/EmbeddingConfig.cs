namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Single-row provenance stamp, written in the same transaction as any embedding or
/// similar_track load. Cosine distance between different model versions is
/// meaningless; this stamp plus the wipe-and-reload discipline is what prevents
/// mixing vector spaces.
/// </summary>
public class EmbeddingConfig
{
    /// <summary>Always true; CHECK (id) makes this a one-row table.</summary>
    public bool Id { get; set; } = true;

    /// <summary>Model + chunking + layer mix, e.g. 'mert-v1-330m/win6s-hop4s/last4'.</summary>
    public string Model { get; set; } = null!;

    public DateTime LoadedAt { get; set; }

    public int TrackCount { get; set; }
}
