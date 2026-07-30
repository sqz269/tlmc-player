using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

public class Disc
{
    public DiscId Id { get; set; }

    public ReleaseId ReleaseId { get; set; }
    public Release Release { get; set; } = null!;

    /// <summary>1-based; unique per release. A single-disc release has exactly one disc row.</summary>
    public short DiscNumber { get; set; }

    public string? Name { get; set; }

    public List<Track> Tracks { get; set; } = [];
}
