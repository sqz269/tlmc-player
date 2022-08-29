using System.ComponentModel.DataAnnotations;

namespace MusicDataService.Models;

public class Asset
{
    [Key]
    [Required]
    public Guid AssetId { get; set; }
    [Required]
    public string AssetName { get; set; }
    [Required]
    public string AssetPath { get; set; }
    public string AssetMime { get; set; }
    public bool Large { get; set; }
}