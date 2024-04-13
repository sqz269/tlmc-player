using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Dtos.Playlist;

public class PlaylistReadDto
{
    public Guid Id { get; set; }
    
    public string Name { get; set; }

    public Guid OwnerId { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public PlaylistType Type { get; set; }

    public DateTime LastModified { get; set; }
}
