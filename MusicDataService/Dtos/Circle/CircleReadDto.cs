namespace MusicDataService.Dtos.Circle;

public class CircleReadDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<string> Alias { get; set; }
}