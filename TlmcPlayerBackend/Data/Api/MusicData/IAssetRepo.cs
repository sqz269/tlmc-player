using Microsoft.EntityFrameworkCore.ChangeTracking;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IAssetRepo
{
    public Task<bool> SaveChanges();
    public Task<Guid> AddAsset(Asset asset);
    public Task<Asset?> GetAssetById(Guid id);

    public EntityEntry<Asset> UpdateAsset(Asset asset);

    public Task<IEnumerable<Asset>> GetAssetsById(IEnumerable<Guid> ids);
}