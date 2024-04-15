using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Dtos.Playlist;

using TlmcPlayerBackend.Models.UserProfile;

public class PlaylistReadDto
{
    public Guid Id { get; set; }
    
    public string Name { get; set; }

    public UserProfile Owner { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public PlaylistType Type { get; set; }

    public DateTime LastModified { get; set; }
}
