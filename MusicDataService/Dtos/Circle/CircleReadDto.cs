using MusicDataService.Models;

namespace MusicDataService.Dtos.Circle;

public class CircleReadDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<string> Alias { get; set; }
    public string? Country { get; set; }
    public List<CircleWebsiteReadDto> Website { get; set; }
    public CircleStatus Status { get; set; }
    public DateTime Established { get; set; }
    public List<string> DataSource { get; set; }
}