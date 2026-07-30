using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.Playlist;

using TlmcPlayerBackend.Models.UserProfile;

public enum PlaylistVisibility
{
    Public,
    Private,
    Unlisted
}

/// <summary>
/// History and Queue are gone from this enum on purpose: history is play_event data,
/// and the queue is its own table (QueueItem) with different invariants.
/// </summary>
public enum PlaylistKind
{
    Normal,
    Favorite,
}

public class Playlist
{
    public PlaylistId Id { get; set; }

    public string Name { get; set; } = null!;

    public UserId OwnerId { get; set; }
    public UserProfile Owner { get; set; } = null!;

    public PlaylistKind Kind { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastModified { get; set; }

    public List<PlaylistItem> Items { get; set; } = [];

    public static Playlist Create(string name, PlaylistVisibility visibility,
        UserId owner,
        PlaylistKind kind = PlaylistKind.Normal)
    {
        return new Playlist
        {
            Id = PlaylistId.New(),

            OwnerId = owner,

            Name = name,
            Visibility = visibility,
            Kind = kind,
            LastModified = DateTime.UtcNow,
            Items = [],
        };
    }
}
