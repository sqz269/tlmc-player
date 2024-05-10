namespace TlmcPlayerBackend.Integrations.Meilisearch.Models;

public class SearchableAlbum
{
    public string Id { get; set; }

    public string Name { get; set; }


    // Duration is in Unix Epoch, (Seconds since 1970-01-01)
    public int? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public IList<string> AlbumArtists { get; set; }

    public IList<string> TrackNames { get; set; }
}