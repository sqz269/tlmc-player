using MusicDataService.Models;
using Nest;

namespace SearchService.Model;

public class Album
{
    [Keyword(Name = "Id")]
    public Guid Id { get; set; }

    [Object(Name = "AlbumName")]
    public LocalizedField AlbumName { get; set; } = new();

    [Date(Name = "ReleaseDate")]
    public DateTime? ReleaseDate { get; set; }

    [Text(Name = "ReleaseConvention")]
    public string ReleaseConvention { get; set; }

    [Text(Name = "CatalogNumber")]
    public string CatalogNumber { get; set; }

    [Number(Name = "NumberOfDiscs")]
    public int? NumberOfDiscs { get; set; }

    [Number(Name = "DiscNumber")]
    public int? DiscNumber { get; set; }

    [Text(Name = "DiscName")]
    public string DiscName { get; set; }

    [Keyword(Name = "Website")]
    public List<string> Website { get; set; } = new List<string>();

    [Nested(Name = "AlbumArtist")]
    public List<Circle> AlbumArtist { get; set; } = new List<Circle>();

    [Keyword(Name = "DataSource")]
    public List<string> DataSource { get; set; } = new List<string>();
}