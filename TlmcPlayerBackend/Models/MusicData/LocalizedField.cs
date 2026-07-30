using System.ComponentModel.DataAnnotations;

namespace TlmcPlayerBackend.Models.MusicData;

public class LocalizedField
{
    [Required]
    public string Default { get; set; } = null!;
    public string? En { get; set; }
    public string? Zh { get; set; }
    public string? Jp { get; set; }
}