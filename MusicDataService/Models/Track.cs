using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class Track
{
    [Key]
    public Guid Id { get; set; }

    [Column(TypeName = "jsonb")]
    [Required]
    public LocalizedField Name { get; set; }

    public int? Index { get; set; }

    public int? Disc { get; set; }

    public List<string>? Genre { get; set; } = new();

    public List<string>? Arrangement { get; set; } = new();

    public List<string>? Vocalist { get; set; } = new();

    public List<string>? Lyricist { get; set; } = new();

    public bool? OriginalNonTouhou { get; set; }
    
    public Album Album { get; set; }

    public List<OriginalTrack>? Original { get; set; } = new();
}