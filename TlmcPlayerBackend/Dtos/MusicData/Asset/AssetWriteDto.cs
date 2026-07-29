using System.ComponentModel.DataAnnotations;

namespace TlmcPlayerBackend.Dtos.MusicData.Asset;

/// <summary>
/// Write shape for the internal asset registration endpoint. Bound in place of the
/// Asset entity so a request cannot reach anything beyond these fields, and so Path
/// can be validated before it is stored -- AssetController opens Path straight off
/// disk, which made an unvalidated value an arbitrary-file-read primitive.
/// </summary>
public class AssetWriteDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(512, MinimumLength = 1)]
    public string Name { get; set; }

    [Required]
    [StringLength(4096, MinimumLength = 1)]
    public string Path { get; set; }

    [StringLength(255)]
    public string? Mime { get; set; }

    [Range(0, long.MaxValue)]
    public long Size { get; set; }
}
