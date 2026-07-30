using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// Which configured storage root a key resolves against. Roots are deployment
/// configuration (Storage:Roots:*); rows hold keys, never paths.
/// </summary>
public enum StorageRoot
{
    Library,
    Thumbnail,
    Generated,
}

public class Asset
{
    public AssetId Id { get; set; }

    public StorageRoot Root { get; set; }

    /// <summary>
    /// Root-relative, forward-slashed, no leading slash. Resolution is
    /// Storage:Roots:&lt;root&gt; + '/' + storage_key, validated by StorageRootResolver.
    /// </summary>
    public string StorageKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Mime { get; set; }

    public long ByteSize { get; set; }

    /// <summary>
    /// Lowercase hex sha256 of the bytes. Enables dedup across TLMC's many duplicate
    /// rips and strong ETags (media is immutable). Nullable: backfilled asynchronously.
    /// </summary>
    public string? ContentHash { get; set; }

    public DateTime CreatedAt { get; set; }
}
