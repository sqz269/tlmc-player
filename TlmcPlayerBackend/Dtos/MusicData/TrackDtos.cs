using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class TrackCreditDto
{
    public CreditRole Role { get; set; }

    /// <summary>
    /// Verbatim from the source (SCHEMA-V6.md section 5). There is no contributor
    /// object; if an identity layer is ever built it arrives as a new optional field.
    /// </summary>
    public string Name { get; set; } = null!;
}

public class TagDto
{
    public TagId Id { get; set; }
    public string Name { get; set; } = null!;
}

public class OriginalSongSlimDto
{
    public OriginalSongId Id { get; set; }
    public LocalizedField Title { get; set; } = null!;
}

public class TrackListItemDto
{
    public TrackId Id { get; set; }
    public short TrackNumber { get; set; }
    public LocalizedField Name { get; set; } = null!;
    public TimeSpan? Duration { get; set; }

    /// <summary>False for catalogue-only tracks (media_key is NULL) — they browse
    /// and search but cannot play.</summary>
    public bool HasMedia { get; set; }

    public bool HasLyrics { get; set; }
}

public class TrackReadDto
{
    public TrackId Id { get; set; }
    public short TrackNumber { get; set; }
    public LocalizedField Name { get; set; } = null!;
    public TimeSpan? Duration { get; set; }
    public bool? OriginalNonTouhou { get; set; }

    public DiscId DiscId { get; set; }
    public short DiscNumber { get; set; }
    public ReleaseSlimDto Release { get; set; } = null!;
    public List<CircleSlimDto> Circles { get; set; } = [];
    public ArtworkId? ArtworkId { get; set; }

    public bool HasMedia { get; set; }
    public List<short> HlsBitrates { get; set; } = [];
    public bool HasDash { get; set; }

    public List<TrackCreditDto> Credits { get; set; } = [];
    public List<TagDto> Tags { get; set; } = [];
    public List<OriginalSongSlimDto> OriginalSongs { get; set; } = [];

    public bool HasLyrics { get; set; }
}
