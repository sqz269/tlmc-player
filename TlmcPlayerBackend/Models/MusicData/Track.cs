using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

public class Track
{
    public TrackId Id { get; set; }

    public DiscId DiscId { get; set; }
    public Disc Disc { get; set; } = null!;

    /// <summary>1-based; unique per disc.</summary>
    public short TrackNumber { get; set; }

    [Column(TypeName = "jsonb")]
    public LocalizedField Name { get; set; } = null!;

    /// <summary>Generated column: lower(name->>'default').</summary>
    public string? NameSort { get; private set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>True when the track is an original composition, not a Touhou arrangement.</summary>
    public bool? OriginalNonTouhou { get; set; }

    /// <summary>
    /// Root-relative track directory in the 'library' storage root. Media URLs are
    /// composed from this plus the layout convention shared with hls_assignment.py.
    /// NULL means the track exists in the catalogue but has no playable media.
    /// </summary>
    public string? MediaKey { get; set; }

    /// <summary>Available HLS rungs, e.g. {128, 192, 256, 320}.</summary>
    public List<short> HlsBitrates { get; set; } = [];

    public bool HasDash { get; set; }

    /// <summary>The source FLAC this track's media was transcoded from.</summary>
    public AssetId? SourceAssetId { get; set; }
    public Asset? SourceAsset { get; set; }

    public LyricsId? LyricsId { get; set; }
    public Lyrics? Lyrics { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<TrackCredit> Credits { get; set; } = [];

    public List<TrackTag> Tags { get; set; } = [];

    public List<TrackOriginalSong> OriginalSongs { get; set; } = [];
}
