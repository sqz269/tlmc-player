using PlaylistService.Model;

namespace PlaylistService.Data.Api;

public interface IPlaylistItemRepo
{
    public Task<int> IncPlaylistItem(Guid playlist, Guid trackId);
    public Task<bool> DoesPlaylistItemExist(Guid playlist, Guid trackId);
    public Task<PlaylistItem> InsertPlaylistItem(Guid playlist, Guid trackId);
    public Task<List<PlaylistItem>> InsertPlaylistItems(Guid playlist, List<Guid> trackIds);
    public Task<PlaylistItem> DeletePlaylistItem(Guid playlist, Guid trackId);
    public Task<List<PlaylistItem>> DeletePlaylistItems(Guid playlist, List<Guid> trackId);
    public Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item);
    public Task<List<PlaylistItem>> GetPlaylistItems(Guid playlist, int start, int limit);
}