using System.ComponentModel.DataAnnotations;

namespace MusicDataService.Models;

public class LocalizedField
{
    [Required]
    public string Default { get; set; }
    public string? En { get; set; }
    public string? Zh { get; set; }
    public string? Jp { get; set; }
}