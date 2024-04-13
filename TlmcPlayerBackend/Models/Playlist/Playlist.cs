using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KeycloakAuthProvider.Identity;

namespace TlmcPlayerBackend.Models.Playlist;

using TlmcPlayerBackend.Models.UserProfile;

public enum PlaylistVisibility
{
    Public,
    Private,
    Unlisted
}

public enum PlaylistType
{
    Normal,

    Favorite,
    History,
    Queue,
}

public class Playlist
{
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid OwnerId { get; set; }
    
    [ForeignKey("OwnerId")]
    public UserProfile Owner { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public PlaylistType Type { get; set; }

    public int NumberOfTracks { get; set; }

    public DateTime LastModified { get; set; }

    public List<PlaylistItem>? Tracks { get; set; } = new();

    public static Playlist Create(string name, PlaylistVisibility visibility,
        UserClaim user, 
        PlaylistType type=PlaylistType.Normal)
    {
        if (user.Username == null || user.UserId == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        return new Playlist
        {
            Id = new Guid(),
            
            OwnerId = user.UserId.Value,
            
            Name = name,
            Visibility = visibility,
            Type = type,
            LastModified = DateTime.UtcNow,
            Tracks = new List<PlaylistItem>()
        };
    }
}