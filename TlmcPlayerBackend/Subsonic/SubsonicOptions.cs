namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// The Subsonic facade is a feature, not a default (Docs/SUBSONIC.md section 12):
/// disabled, every /rest route answers 404 and nothing else changes.
/// </summary>
public class SubsonicOptions
{
    public const string Section = "Subsonic";

    public bool Enabled { get; set; }

    /// <summary>
    /// Requests with no credentials get the read-only surface; user-scoped
    /// endpoints answer error 50. Many clients refuse setup without credentials,
    /// so this is a bonus for public instances, not the primary mode.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// When set, `stream`/`download` hand out the source FLAC for unconstrained
    /// requests instead of the highest AAC rung (Docs/SUBSONIC.md section 5).
    /// </summary>
    public bool ServeLossless { get; set; }
}
