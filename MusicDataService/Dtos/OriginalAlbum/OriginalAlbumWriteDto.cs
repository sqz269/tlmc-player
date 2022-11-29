using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MusicDataService.Models;

namespace MusicDataService.Dtos.OriginalAlbum;

public class OriginalAlbumWriteDto
{
    [Required]
    public string Id { get; set; }

    [Required]
    public string Type { get; set; }

    [Required]
    public LocalizedField FullName { get; set; }

    [Required]
    public LocalizedField ShortName { get; set; }

    public string ExternalReference { get; set; }
}