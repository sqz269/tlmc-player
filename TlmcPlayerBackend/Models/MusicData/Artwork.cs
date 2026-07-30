using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// A cover image and its resized variants. Replaces the five-FK Thumbnail table:
/// adding a size is an insert, not a migration.
/// </summary>
public class Artwork
{
    public ArtworkId Id { get; set; }

    public AssetId SourceAssetId { get; set; }
    public Asset SourceAsset { get; set; } = null!;

    /// <summary>Dominant colours, for UI theming.</summary>
    public List<string> Colors { get; set; } = [];

    public List<ArtworkVariant> Variants { get; set; } = [];
}

public class ArtworkVariant
{
    public ArtworkId ArtworkId { get; set; }
    public Artwork Artwork { get; set; } = null!;

    /// <summary>Edge length in px; 0 means the unresized original.</summary>
    public short SizePx { get; set; }

    public AssetId AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
}
