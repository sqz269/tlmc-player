using System.ComponentModel.DataAnnotations;
namespace MusicDataService.Models;

public class HlsSegment
{
    [Key]
    public Guid Id { get; set; }
    
    public int Index { get; set; }
    
    public string Name { get; set; }
    
    public string HlsSegmentPath { get; set; }

    public Guid HlsPlaylistId { get; set; }
    public HlsPlaylist HlsPlaylist { get; set; }
}