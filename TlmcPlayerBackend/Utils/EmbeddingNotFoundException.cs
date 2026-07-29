namespace TlmcPlayerBackend.Utils;

/// <summary>
/// Raised when a track has no embedding row. This is an ordinary, expected state --
/// embeddings are produced by a separate inference pipeline that lags ingestion --
/// so it is a distinct type rather than a bare Exception, letting the similar-tracks
/// endpoint answer 404 instead of 500.
/// </summary>
public sealed class EmbeddingNotFoundException(Guid trackId)
    : Exception($"No embedding found for track with id: {trackId}")
{
    public Guid TrackId { get; } = trackId;
}
