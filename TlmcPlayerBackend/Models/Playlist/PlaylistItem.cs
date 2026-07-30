using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Models.Playlist;

/// <summary>
/// PK (playlist_id, track_id) is a deduplicating key on purpose: a playlist holding
/// the same track twice stays disallowed. Duplicates belong to the queue (QueueItem).
/// The position unique constraint is DEFERRABLE (added via raw SQL in the initial
/// migration) so renumbering can shuffle positions inside one transaction.
/// </summary>
public class PlaylistItem
{
    public PlaylistId PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    /// <summary>1-based, dense per playlist.</summary>
    public int Position { get; set; }

    public DateTime AddedAt { get; set; }
}
