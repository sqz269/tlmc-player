using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>A Touhou game/album the music originates from. Was OriginalAlbum (text PK).</summary>
public class OriginalWork
{
    public OriginalWorkId Id { get; set; }

    /// <summary>The old '{csvSourceId}' natural key, kept for the thwiki pipeline.</summary>
    public string ExternalKey { get; set; } = null!;

    public string WorkType { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public LocalizedField FullName { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public LocalizedField ShortName { get; set; } = null!;

    public string? ExternalRef { get; set; }

    public List<OriginalSong> Songs { get; set; } = [];
}
