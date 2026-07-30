using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>An original Touhou song. Was OriginalTrack (text PK '{csvSourceId}-{index}').</summary>
public class OriginalSong
{
    public OriginalSongId Id { get; set; }

    public OriginalWorkId OriginalWorkId { get; set; }
    public OriginalWork OriginalWork { get; set; } = null!;

    /// <summary>The old '{csvSourceId}-{index}' natural key, kept for the thwiki pipeline.</summary>
    public string ExternalKey { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public LocalizedField Title { get; set; } = null!;

    public short? TrackIndex { get; set; }

    public string? ExternalRef { get; set; }

    public List<TrackOriginalSong> Arrangements { get; set; } = [];
}

/// <summary>Which original songs a track arranges. Was OriginalTrackTrack.</summary>
public class TrackOriginalSong
{
    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public OriginalSongId OriginalSongId { get; set; }
    public OriginalSong OriginalSong { get; set; } = null!;
}
