using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.UserProfile;

/// <summary>
/// A per-user key for the Subsonic facade (Docs/SUBSONIC.md section 4). The key
/// string itself (`tlmc_...`) is shown once at creation and never stored — only
/// its sha256 — so a database read cannot recover a credential.
/// </summary>
public class ApiKey
{
    public ApiKeyId Id { get; set; }

    public UserId UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    /// <summary>User-facing label ("kitchen tablet"), 1–100 chars.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Lowercase hex sha256 of the full key string.</summary>
    public string KeyHash { get; set; } = null!;

    /// <summary>First characters of the key, for recognizing it in a list.</summary>
    public string KeyPrefix { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    /// <summary>Coarse (see SubsonicAuth): bumped at most once per hour.</summary>
    public DateTime? LastUsedAt { get; set; }
}
