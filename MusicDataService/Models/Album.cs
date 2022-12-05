using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class Album
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public LocalizedField AlbumName { get; set; } = new();

    [Column(TypeName = "date")]
    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    [Required]
    public int? NumberOfDiscs { get; set; }

    public List<string>? Website { get; set; } = new();

    [Required]
    public List<Circle>? AlbumArtist { get; set; } = new();

    public List<string>? DataSource { get; set; } = new();

    public List<Track>? Tracks { get; set; } = new();

    public List<Album>? LinkedAlbums { get; set; } = new();

    public Asset? AlbumImage { get; set; }

    public Thumbnail? Thumbnail { get; set; }

    public List<Asset> OtherFiles { get; set; } = new();
}