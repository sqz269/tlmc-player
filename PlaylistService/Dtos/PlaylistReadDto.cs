using PlaylistService.Model;

namespace PlaylistService.Dtos;

public class PlaylistReadDto
{
    public Guid Id { get; set; }
    
    public string Name { get; set; }

    public Guid UserId { get; set; }

    public string Username { get; set; }

    public PlaylistVisibility Visibility { get; set; }

    public PlaylistType Type { get; set; }

    public DateTime LastModified { get; set; }
}
