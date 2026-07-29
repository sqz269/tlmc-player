namespace TlmcPlayerBackend.Data.Api.Playlist;

public interface IPlaylistRepo
{
    public Task<bool> SaveChanges();
    public Task<bool> DoesPersonalPlaylistExist(Guid userId);
    public Task<IEnumerable<Models.Playlist.Playlist>> GetUserPlaylist(Guid ownerId, Guid? userId);

    /// <summary>
    /// Read-scoped lookup: resolves the playlist if <paramref name="userId"/> owns it
    /// OR it is publicly visible. Suitable for reads only -- passing this check does
    /// NOT establish that the caller owns the playlist, so every mutating path must
    /// use <see cref="GetOwnedPlaylist"/> instead.
    /// </summary>
    public Task<Models.Playlist.Playlist?> GetPlaylist(Guid playlistId, Guid? userId);

    /// <summary>
    /// Write-scoped lookup: resolves the playlist only if <paramref name="ownerId"/>
    /// owns it, whatever its visibility.
    /// </summary>
    public Task<Models.Playlist.Playlist?> GetOwnedPlaylist(Guid playlistId, Guid ownerId);

    public Task<Models.Playlist.Playlist?> GetPlaylist(Guid playlistId, bool noTracking = false);
    public Task<Models.Playlist.Playlist?> GetHistoryPlaylist(Guid userId);
    public Task<Models.Playlist.Playlist?> GetFavoritesPlaylist(Guid userId);
    public Task<Models.Playlist.Playlist?> GetQueuePlaylist(Guid userId);

    public Task<Models.Playlist.Playlist?> InsertPlaylist(Models.Playlist.Playlist playlist);
    public Task<bool> InsertPlaylists(IEnumerable<Models.Playlist.Playlist> playlists);

    /// <summary>
    /// Deletes the playlist only if <paramref name="ownerId"/> owns it. The owner
    /// filter lives here as well as in the controller, so a caller that forgets the
    /// ownership check still cannot delete someone else's playlist.
    /// </summary>
    public Task<bool> DeletePlaylist(Guid playlistId, Guid ownerId);
}
