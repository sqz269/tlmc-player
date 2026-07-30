using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class CircleSlimDto
{
    public CircleId Id { get; set; }
    public string Name { get; set; } = null!;
}

public class CircleWebsiteDto
{
    public string Url { get; set; } = null!;
    public bool Invalid { get; set; }
}

public class CircleReadDto
{
    public CircleId Id { get; set; }
    public string Name { get; set; } = null!;
    public CircleStatus Status { get; set; }
    public DateOnly? Established { get; set; }
    public string? Country { get; set; }
    public List<string> Alias { get; set; } = [];
    public List<CircleWebsiteDto> Website { get; set; } = [];
}
