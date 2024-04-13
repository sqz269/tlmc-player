using System.ComponentModel.DataAnnotations;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.OriginalTrack;

public class OriginalTrackWriteDto
{
    [Required]
    public string Id { get; set; }

    [Required]
    public LocalizedField Title { get; set; }

    public string? ExternalReference { get; set; }
}