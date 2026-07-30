using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Pooled MERT vectors. There is deliberately no per-row model version: exactly one
/// model is live per database, and provenance lives in the single-row EmbeddingConfig
/// stamped in the same transaction as any load (see SCHEMA-V6.md section 9).
/// </summary>
public class TrackEmbedding
{
    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    /// <summary>'mean' pooling.</summary>
    [Column(TypeName = "vector(1024)")]
    public Vector EmbeddingMean { get; set; } = null!;

    // halfvec, not vector: pgvector's HNSW/IVFFlat indexes cap out at 2000 dims
    // for vector but 4000 for halfvec. As vector(2048) this column could never
    // be ANN-indexed and every similarity query was a sequential scan. fp16 is
    // ample precision for a recall stage.
    /// <summary>'mean+max' pooling.</summary>
    [Column(TypeName = "halfvec(2048)")]
    public HalfVector EmbeddingMeanMax { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
