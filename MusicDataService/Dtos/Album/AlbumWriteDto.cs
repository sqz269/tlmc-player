using System.ComponentModel.DataAnnotations;
using MusicDataService.Models;

namespace MusicDataService.Dtos.Album;

public class AlbumWriteDto
{
    [Required]
    public LocalizedField AlbumName { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public int? NumberOfDiscs { get; set; }

    public string? Website { get; set; }

    public List<string>? AlbumArtist { get; set; }

    public List<string>? Genre { get; set; }

    public List<string>? DataSource { get; set; }

    // Needs to be converted into Asset
    public Guid? AlbumImage { get; set; }

    public List<Guid>? OtherFiles { get; set; }
}
