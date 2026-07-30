using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

/// <summary>
/// A published release (an "album" in the doujin sense). Discs are separate rows —
/// the v5 dual-purpose Album row and its DiscNumber sentinel are gone.
/// </summary>
public class Release
{
    public ReleaseId Id { get; set; }

    [Column(TypeName = "jsonb")]
    public LocalizedField Name { get; set; } = null!;

    /// <summary>Generated column: lower(name->>'default'). jsonb is the wrong thing to ORDER BY.</summary>
    public string? NameSort { get; private set; }

    public DateOnly? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public List<string> Websites { get; set; } = [];

    public List<string> DataSources { get; set; } = [];

    public List<string> TlmcRootReference { get; set; } = [];

    public ArtworkId? ArtworkId { get; set; }
    public Artwork? Artwork { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Disc> Discs { get; set; } = [];

    public List<ReleaseCircle> Circles { get; set; } = [];
}

/// <summary>Release attribution, ordered. Replaces the implicit AlbumCircle join.</summary>
public class ReleaseCircle
{
    public ReleaseId ReleaseId { get; set; }
    public Release Release { get; set; } = null!;

    public CircleId CircleId { get; set; }
    public Circle Circle { get; set; } = null!;

    public short Ordinal { get; set; }
}
