using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class OriginalAlbum
{
    [Key]
    public string Id { get; set; }

    public string Type { get; set; }

    [Column(TypeName = "jsonb")]
    public LocalizedField FullName { get; set; }

    [Column(TypeName = "jsonb")]
    public LocalizedField ShortName { get; set; }

    public string ExternalReference { get; set; }
    
    public List<OriginalTrack> Tracks { get; set; } = new();
}