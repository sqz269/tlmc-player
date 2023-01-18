using PlaylistService.Model;

namespace PlaylistService.Data.Api;

public interface IPlaylistItemRepo
{
    public Task<int> IncPlaylistItem(Guid playlist, Guid trackId);
    public Task<bool> DoesPlaylistItemExist(Guid playlist, Guid trackId);
    public Task<PlaylistItem> InsertPlaylistItem(Guid playlist, Guid trackId);
    public Task<PlaylistItem> DeletePlaylistItem(Guid playlist, Guid trackId);
    public Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item);
}