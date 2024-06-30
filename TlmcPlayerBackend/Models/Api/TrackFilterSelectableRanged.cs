namespace TlmcPlayerBackend.Models.Api;

public class TrackFilterSelectableRanged
{
    public DateTime? ReleaseDateBegin { get; set; } // Applies as an AND filter
    public DateTime? ReleaseDateEnd { get; set; } // Applies as an AND filter

    public List<Guid>? CircleIds { get; set; } // Applies as an OR filter

    public List<string>? OriginalAlbumIds { get; set; } // Applies as an OR filter

    public List<string>? OriginalTrackIds { get; set; } // Applies as an OR filter

    public bool IsEmpty()
    {
        return ReleaseDateBegin == null && ReleaseDateEnd == null && CircleIds == null && OriginalAlbumIds == null && OriginalTrackIds == null;
    }
}
