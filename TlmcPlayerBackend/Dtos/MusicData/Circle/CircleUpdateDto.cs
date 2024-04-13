using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.Circle;

public class CircleUpdateDto
{
    public CircleStatus Status { get; set; }
    public DateTime Established { get; set; }
    public List<string> Alias { get; set; }
    public List<CircleWebsiteUpdateDto> Website { get; set; }
}