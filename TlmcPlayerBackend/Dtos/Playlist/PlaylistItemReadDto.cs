namespace TlmcPlayerBackend.Dtos.Playlist;

public class PlaylistItemReadDto
{
    public Guid TrackId { get; set; }
    public int Index { get; set; }
    public int TimesPlayed { get; set; }
    public DateTime DateAdded { get; set; }
}