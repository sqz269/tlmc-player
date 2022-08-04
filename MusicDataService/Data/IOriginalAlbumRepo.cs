using MusicDataService.Models;

namespace MusicDataService.Data;

public interface IOriginalAlbumRepo
{
    public Task<bool> SaveChanges();

    public Task<OriginalAlbum?> GetOriginalAlbum(string id);

    public Task<string> AddOriginalAlbum(OriginalAlbum originalAlbum);
}