using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class ArtworkVariantDto
{
    /// <summary>Edge length in px; 0 is the unresized original.</summary>
    public short SizePx { get; set; }

    public AssetId AssetId { get; set; }
}

public class ArtworkReadDto
{
    public ArtworkId Id { get; set; }
    public List<string> Colors { get; set; } = [];
    public List<ArtworkVariantDto> Variants { get; set; } = [];
}
