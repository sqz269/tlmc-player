using PlaylistService.Model;

namespace PlaylistService.Data.Api;

public interface IPlaylistRepo
{
    public Task<bool> SaveChanges();

    public Task<IEnumerable<Playlist>> GetUserPlaylist(Guid ownerId, Guid? userId);
    public Task<Playlist?> GetPlaylist(Guid playlistId, Guid? userId);
    public Task<Playlist?> GetPlaylist(Guid playlistId, bool noTracking=false);
    public Task<Playlist?> GetHistoryPlaylist(Guid userId);
    public Task<Playlist?> GetFavoritesPlaylist(Guid userId);
    public Task<Playlist?> GetQueuePlaylist(Guid userId);

    public Task<Playlist?> InsertPlaylist(Playlist playlist);
}