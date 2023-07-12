using Microsoft.EntityFrameworkCore.ChangeTracking;
using MusicDataService.Models;

namespace MusicDataService.Data.Api;

public interface IAssetRepo
{
    public Task<bool> SaveChanges();
    public Task<Guid> AddAsset(Asset asset);
    public Task<Asset?> GetAssetById(Guid id);

    public EntityEntry<Asset> UpdateAsset(Asset asset);

    public Task<IEnumerable<Asset>> GetAssetsById(IEnumerable<Guid> ids);
}