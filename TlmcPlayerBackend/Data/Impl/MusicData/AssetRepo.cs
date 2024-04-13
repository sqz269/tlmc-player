using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Impl.MusicData;

public class AssetRepo : IAssetRepo
{
    private readonly AppDbContext _context;

    public AssetRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 0;
    }

    public async Task<Asset?> GetAssetById(Guid id)
    {
        return await _context.Assets.Where(a => a.Id == id).FirstOrDefaultAsync();
    }

    public EntityEntry<Asset> UpdateAsset(Asset asset)
    {
        return _context.Assets.Update(asset);
    }

    public async Task<IEnumerable<Asset>> GetAssetsById(IEnumerable<Guid> ids)
    {
        return await _context.Assets.Where(a => ids.Contains(a.Id)).ToListAsync();
    }

    public async Task<Guid> AddAsset(Asset asset)
    {
        if (asset.Id == Guid.Empty)
        {
            asset.Id = Guid.NewGuid();
        }

        var result = await _context.Assets.AddAsync(asset);
        return result.Entity.Id;
    }
}