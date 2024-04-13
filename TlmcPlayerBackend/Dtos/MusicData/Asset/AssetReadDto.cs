namespace TlmcPlayerBackend.Dtos.MusicData.Asset;

public class AssetReadDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; }
    public string AssetMime { get; set; }
    public bool Large { get; set; }
    public long Size { get; set; }
    public string Url { get; set; }
}