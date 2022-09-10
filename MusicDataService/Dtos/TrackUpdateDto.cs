using System.ComponentModel.DataAnnotations;
using MusicDataService.Models;

namespace MusicDataService.Dtos;

public class TrackUpdateDto
{
    //[Required]
    //public LocalizedField Name { get; set; }

    public List<string>? Genre { get; set; }

    public List<string>? Staff { get; set; }

    public List<string>? Arrangement { get; set; }

    public List<string>? Vocalist { get; set; }

    public List<string>? Lyricist { get; set; }

    public List<string>? Original { get; set; }

    public bool? OriginalNonTouhou { get; set; }
}