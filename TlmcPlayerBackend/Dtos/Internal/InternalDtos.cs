using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.Internal;

public class CircleUpsertDto
{
    public required string Name { get; set; }
    public CircleStatus? Status { get; set; }
    public DateOnly? Established { get; set; }
    public string? Country { get; set; }
    public List<string>? Alias { get; set; }
    public List<CircleWebsiteUpsertDto>? Websites { get; set; }
}

public class CircleWebsiteUpsertDto
{
    public required string Url { get; set; }
    public bool Invalid { get; set; }
}

public class TrackOriginalsWriteDto
{
    /// <summary>The '{csvSourceId}-{index}' natural keys the thwiki pipeline speaks.</summary>
    public required List<string> SongExternalKeys { get; set; }
}
