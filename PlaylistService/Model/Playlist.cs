using System.ComponentModel.DataAnnotations;
using AuthServiceClientApi;

namespace PlaylistService.Model;

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

    public Guid UserId { get; set; }

    public string Username { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public PlaylistType Type { get; set; }

    public DateTime LastModified { get; set; }

    public int NumberOfTracks { get; set; }

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
            
            UserId = user.UserId.Value,
            Username = user.Username,
            
            Name = name,
            Visibility = visibility,
            Type = type,
            LastModified = DateTime.Now,
            NumberOfTracks = 0,
            Tracks = new List<PlaylistItem>()
        };
    }
}