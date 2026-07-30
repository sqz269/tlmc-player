using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Dtos.Playlist;

public class QueueItemDto
{
    /// <summary>Queue entry id — the same track can appear twice, so the track id
    /// alone cannot address an entry.</summary>
    public long Id { get; set; }

    public int Position { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public ArtworkId? ArtworkId { get; set; }
}

public class QueueWriteDto
{
    public required List<TrackId> TrackIds { get; set; }
}

public class QueueEnqueueDto
{
    public required List<TrackId> TrackIds { get; set; }

    /// <summary>True inserts at the front ("play next"); false appends.</summary>
    public bool AtFront { get; set; }
}
