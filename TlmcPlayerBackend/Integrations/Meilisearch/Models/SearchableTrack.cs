namespace TlmcPlayerBackend.Integrations.Meilisearch.Models;

public class SearchableTrack
{
    public string Id { get; set; }

    public string Name { get; set; }

    public int Index { get; set; }

    public int Disc { get; set; }

    // Duration is in seconds
    public int Duration { get; set; }

    public IList<string> Genre { get; set; }

    public IList<string> Staff { get; set; }

    public IList<string> Arrangement { get; set; }

    public IList<string> Vocalist { get; set; }

    public IList<string> Lyricist { get; set; }

    public bool OriginalNonTouhou { get; set; }

    public List<string> OriginalTracksPk { get; set; }
    public List<string> OriginalTracksJp { get; set; }
    public List<string> OriginalTracksEn { get; set; }
    public List<string> OriginalTracksZh { get; set; }

    public List<string> OriginalAlbumsPk { get; set; }
    public List<string> OriginalAlbumsJp { get; set; }
    public List<string> OriginalAlbumsEn { get; set; }
    public List<string> OriginalAlbumsZh { get; set; }

    // Meilisearch does not like multiple fields with Ids at the end
    public string Album { get; set; }
}