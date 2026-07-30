using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class ReleaseSlimDto
{
    public ReleaseId Id { get; set; }
    public LocalizedField Name { get; set; } = null!;
}

public class ReleaseListItemDto
{
    public ReleaseId Id { get; set; }
    public LocalizedField Name { get; set; } = null!;
    public DateOnly? ReleaseDate { get; set; }
    public string? ReleaseConvention { get; set; }
    public string? CatalogNumber { get; set; }
    public List<CircleSlimDto> Circles { get; set; } = [];
    public ArtworkId? ArtworkId { get; set; }
    public int TrackCount { get; set; }
}

public class ReleaseReadDto
{
    public ReleaseId Id { get; set; }
    public LocalizedField Name { get; set; } = null!;
    public DateOnly? ReleaseDate { get; set; }
    public string? ReleaseConvention { get; set; }
    public string? CatalogNumber { get; set; }
    public List<string> Websites { get; set; } = [];
    public List<string> DataSources { get; set; } = [];
    public List<CircleSlimDto> Circles { get; set; } = [];
    public ArtworkReadDto? Artwork { get; set; }
    public List<DiscReadDto> Discs { get; set; } = [];
}

public class DiscReadDto
{
    public DiscId Id { get; set; }
    public short DiscNumber { get; set; }
    public string? Name { get; set; }
    public List<TrackListItemDto> Tracks { get; set; } = [];
}
