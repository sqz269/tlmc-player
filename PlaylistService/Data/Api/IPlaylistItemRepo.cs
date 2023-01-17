using PlaylistService.Model;

namespace PlaylistService.Data.Api;

public interface IPlaylistItemRepo
{
    public Task<PlaylistItem> InsertPlaylistItem(Guid playlist, Guid trackId);
    public Task<PlaylistItem> DeletePlaylistItem(Guid playlist, Guid trackId);
    public Task<PlaylistItem> GetPlaylistItem(Guid playlist, Guid item);
}