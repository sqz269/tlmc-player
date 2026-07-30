using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Models.Playlist;

using TlmcPlayerBackend.Models.UserProfile;

/// <summary>
/// The play queue is not a playlist: duplicates are natural, there is exactly one per
/// user, it is never public, and it churns on every skip. Future metadata (enqueue
/// source, originating context, played flag) lands here without touching playlists.
/// </summary>
public class QueueItem
{
    public long Id { get; set; }

    public UserId UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    /// <summary>1-based; unique per user (DEFERRABLE, via raw SQL in the migration).</summary>
    public int Position { get; set; }

    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public DateTime EnqueuedAt { get; set; }
}
