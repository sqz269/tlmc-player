namespace TlmcPlayerBackend.Data.Api.Playlist;

public interface IPlaylistRepo
{
    public Task<bool> SaveChanges();
    public Task<bool> DoesPersonalPlaylistExist(Guid userId);
    public Task<IEnumerable<Models.Playlist.Playlist>> GetUserPlaylist(Guid ownerId, Guid? userId);
    public Task<Models.Playlist.Playlist?> GetPlaylist(Guid playlistId, Guid? userId);
    public Task<Models.Playlist.Playlist?> GetPlaylist(Guid playlistId, bool noTracking=false);
    public Task<Models.Playlist.Playlist?> GetHistoryPlaylist(Guid userId);
    public Task<Models.Playlist.Playlist?> GetFavoritesPlaylist(Guid userId);
    public Task<Models.Playlist.Playlist?> GetQueuePlaylist(Guid userId);

    public Task<Models.Playlist.Playlist?> InsertPlaylist(Models.Playlist.Playlist playlist);
    public Task<bool> InsertPlaylists(IEnumerable<Models.Playlist.Playlist> playlists);
    public Task<bool> DeletePlaylist(Guid playlistId);
}