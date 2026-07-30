using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Dtos.Playlist;

public class PlaylistReadDto
{
    public PlaylistId Id { get; set; }
    public string Name { get; set; } = null!;
    public PlaylistKind Kind { get; set; }
    public PlaylistVisibility Visibility { get; set; }
    public UserId OwnerId { get; set; }
    public string OwnerDisplayName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }

    /// <summary>Derived (COUNT), never stored — the v5 NumberOfTracks drifted.</summary>
    public int TrackCount { get; set; }
}

public class PlaylistWriteDto
{
    public required string Name { get; set; }
    public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;
}

public class PlaylistItemDto
{
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public ArtworkId? ArtworkId { get; set; }
}

public class PlaylistItemsWriteDto
{
    public required List<TrackId> TrackIds { get; set; }
}

public class PlaylistItemMoveDto
{
    public TrackId TrackId { get; set; }

    /// <summary>1-based target position; clamped to the playlist's length.</summary>
    public int ToPosition { get; set; }
}
