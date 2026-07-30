using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>Genre vocabulary; a tag is a vocabulary entry, not a credit.</summary>
public class Tag
{
    public TagId Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Generated column: lower(name).</summary>
    public string? NameSort { get; private set; }

    public List<TrackTag> Tracks { get; set; } = [];
}

public class TrackTag
{
    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public TagId TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
