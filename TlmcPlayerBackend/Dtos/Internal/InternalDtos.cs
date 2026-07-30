using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.Internal;

public class CircleUpsertDto
{
    public required string Name { get; set; }
    public CircleStatus? Status { get; set; }
    public DateOnly? Established { get; set; }
    public string? Country { get; set; }
    public List<string>? Alias { get; set; }
    public List<CircleWebsiteUpsertDto>? Websites { get; set; }
}

public class CircleWebsiteUpsertDto
{
    public required string Url { get; set; }
    public bool Invalid { get; set; }
}

public class TrackOriginalsWriteDto
{
    /// <summary>The '{csvSourceId}-{index}' natural keys the thwiki pipeline speaks.</summary>
    public required List<string> SongExternalKeys { get; set; }
}

public class TrackCreditGroupDto
{
    public required CreditRole Role { get; set; }
    /// <summary>Verbatim names, source order. Blanks are dropped, duplicates collapsed.</summary>
    public required List<string> Names { get; set; }
}

/// <summary>
/// Replaces a track's credit rows for exactly the roles present in the payload;
/// roles not mentioned (the loader's staff rows, notably) are left untouched.
/// </summary>
public class TrackCreditsWriteDto
{
    public required List<TrackCreditGroupDto> Credits { get; set; }
}

/// <summary>
/// Enrichment from an external source: catalog number fills only when the local
/// metadata had none (the collection's own tagging wins), website and data source
/// append set-wise.
/// </summary>
public class ReleaseSourceMetaWriteDto
{
    public string? CatalogNumber { get; set; }
    public string? Website { get; set; }
    public string? DataSource { get; set; }
}
