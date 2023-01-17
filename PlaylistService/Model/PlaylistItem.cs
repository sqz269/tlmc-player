using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlaylistService.Model;

[PrimaryKey(nameof(TrackId), nameof(PlaylistId))]
public class PlaylistItem
{
    [Required]
    public Guid TrackId { get; set; }
    public int Index { get; set; }
    public int TimesPlayed { get; set; }
    public DateTime DateAdded { get; set; }

    [Required]
    [ForeignKey("Playlist")]
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; }
}