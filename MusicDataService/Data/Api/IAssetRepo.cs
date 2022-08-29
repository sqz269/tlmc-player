using MusicDataService.Models;

namespace MusicDataService.Data.Api;

public interface IAssetRepo
{
    public Task<bool> SaveChanges();
    public Task<Guid> AddAsset(Asset asset);
    public Task<Asset?> GetAssetById(Guid id);
}