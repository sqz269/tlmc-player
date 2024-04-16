namespace TlmcPlayerBackend.Dtos.MusicData.Asset;

public class AssetReadDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Mime { get; set; }
    public long Size { get; set; }
    public string Url { get; set; }
}