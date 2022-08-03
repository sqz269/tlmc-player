using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class Album
{
    [Key]
    public Guid Id { get; set; }

    public List<Guid>? LinkedAlbums { get; set; } = new();

    [Column(TypeName = "jsonb")]
    public LocalizedField AlbumName { get; set; } = new();

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public int? NumberOfDiscs { get; set; }

    public string? Website { get; set; }

    public List<string>? AlbumArtist { get; set; } = new();

    public List<string>? Genre { get; set; } = new();

    public List<string>? DataSource { get; set; } = new();

    public List<Track>? Tracks { get; set; } = new();
}