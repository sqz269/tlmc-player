using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Models.UserProfile;

namespace TlmcPlayerBackend.Models.Playlist;

/// <summary>
/// One recommendation shown to a user (Docs/RECOMMENDER.md section 3c).
/// Append-only: surfaces write rows and nothing updates them; the
/// rec_attribution view joins them to play_event to decide converted /
/// completed / skipped, so conversion thresholds live in exactly one place.
/// </summary>
public class RecImpression
{
    public long Id { get; set; }

    public UserId UserId { get; set; }
    public UserProfile.UserProfile User { get; set; } = null!;

    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    /// <summary>Surface tag, e.g. "home.because", "radio", "mix.family".</summary>
    public string Surface { get; set; } = null!;

    /// <summary>Surface-specific payload: anchor id, session id, family, batch.</summary>
    [Column(TypeName = "jsonb")]
    public string? Context { get; set; }

    public DateTime ServedAt { get; set; }
}
