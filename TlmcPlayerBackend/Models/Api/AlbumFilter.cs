namespace TlmcPlayerBackend.Models.Api;

public class AlbumFilter
{
    public string? Title { get; set; }
    public DateTime? ReleaseDateBegin { get; set; }
    public DateTime? ReleaseDateEnd { get; set; }
    public string? Convention { get; set; }
    public string? Catalog { get; set; }
    public string? Artist { get; set; }
    public Guid? ArtistId { get; set; }
}
