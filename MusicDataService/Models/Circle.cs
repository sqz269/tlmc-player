using System.ComponentModel.DataAnnotations;

namespace MusicDataService.Models;

public class Circle
{
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; }
    public List<string> Alias { get; set; }

    public List<Album> Albums { get; set; } = new();
}