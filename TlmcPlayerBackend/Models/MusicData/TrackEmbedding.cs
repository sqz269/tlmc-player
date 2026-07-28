using Pgvector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TlmcPlayerBackend.Models.MusicData;

public class TrackEmbedding
{
    [Key, ForeignKey(nameof(Track))]
    public Guid TrackId { get; set; }

    [Column(TypeName = "vector(1024)")]
    public Vector EmbeddingMean { get; set; }

    // halfvec, not vector: pgvector's HNSW/IVFFlat indexes cap out at 2000 dims
    // for vector but 4000 for halfvec. As vector(2048) this column could never
    // be ANN-indexed and every similarity query was a sequential scan. fp16 is
    // ample precision for a recall stage.
    [Column(TypeName = "halfvec(2048)")]
    public HalfVector EmbeddingMeanMax { get; set; }

    public Track Track { get; set; }
}

