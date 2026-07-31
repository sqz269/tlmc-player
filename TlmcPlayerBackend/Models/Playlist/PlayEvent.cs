using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Models.Playlist;

using TlmcPlayerBackend.Models.UserProfile;

public enum PlaySource
{
    Playlist,
    Album,
    Shuffle,
    Similar,
    Search,
    Unknown,
    // Docs/RECOMMENDER.md section 3b: attribution vocabulary for the
    // recommendation surfaces. Postgres enum values are append-only.
    Radio,
    Recommended,
    Map,
    Queue,
}

/// <summary>
/// Append-only play history. Replaces the History playlist kind and the unreachable
/// PlaylistItem.TimesPlayed counter: history is ORDER BY played_at DESC, play counts
/// are an indexed aggregate.
/// </summary>
public class PlayEvent
{
    public long Id { get; set; }

    public UserId UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public DateTime PlayedAt { get; set; }

    public PlaySource Source { get; set; }

    public int? MsPlayed { get; set; }
}
