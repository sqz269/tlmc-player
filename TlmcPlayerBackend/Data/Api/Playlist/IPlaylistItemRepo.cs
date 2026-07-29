using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Data.Api.Playlist;

public interface IPlaylistItemRepo
{
    public Task<int> IncPlaylistItem(Guid playlist, Guid trackId);
    public Task<List<Guid>> DoesPlaylistItemsExistInPlaylist(Guid playlistId, List<Guid> trackIds);

    /// <summary>
    /// Returns the subset of <paramref name="trackIds"/> that are not real tracks, so
    /// callers can reject them before the database does.
    /// </summary>
    public Task<List<Guid>> GetUnknownTrackIds(List<Guid> trackIds);

    public Task<PlaylistItem?> InsertPlaylistItem(Guid playlist, Guid trackId);
    public Task<List<PlaylistItem>> InsertPlaylistItems(Guid playlist, List<Guid> trackIds);
    public Task<PlaylistItem?> DeletePlaylistItem(Guid playlist, Guid trackId);
    public Task<List<PlaylistItem>> DeletePlaylistItems(Guid playlist, List<Guid> trackId);
    public Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item);
    public Task<List<PlaylistItem>> GetPlaylistItems(Guid playlist, int start, int limit);
}
