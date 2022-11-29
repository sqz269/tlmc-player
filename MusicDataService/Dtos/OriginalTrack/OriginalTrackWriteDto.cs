using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MusicDataService.Models;

namespace MusicDataService.Dtos.OriginalTrack;

public class OriginalTrackWriteDto
{
    [Required]
    public string Id { get; set; }

    [Required]
    public LocalizedField Title { get; set; }

    public string? ExternalReference { get; set; }
}