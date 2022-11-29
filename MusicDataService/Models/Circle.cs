using System.ComponentModel.DataAnnotations;

namespace MusicDataService.Models;

public class Circle
{
    [Key]
    public string Name { get; set; }
    public List<string> Alias { get; set; }
}