using System.ComponentModel.DataAnnotations;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.OriginalAlbum;

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