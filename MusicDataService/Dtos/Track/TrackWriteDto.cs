using System.ComponentModel.DataAnnotations;
using MusicDataService.Models;

namespace MusicDataService.Dtos.Track;

public class TrackWriteDto
{
    [Required]
    public LocalizedField Name { get; set; }

    [Required]
    public int? Index { get; set; }

    [Required]
    public int? Disc { get; set; }

    public List<string>? Genre { get; set; }

    public List<string>? Staff { get; set; }

    public List<string>? Arrangement { get; set; }

    public List<string>? Vocalist { get; set; }

    public List<string>? Lyricist { get; set; }

    public List<string>? Original { get; set; }

    public bool? OriginalNonTouhou { get; set; }

    public Guid? TrackFile { get; set; }
}
