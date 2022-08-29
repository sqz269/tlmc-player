using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

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
        return await _context.Assets.Where(a => a.AssetId == id).FirstOrDefaultAsync();
    }

    public async Task<Guid> AddAsset(Asset asset)
    {
        if (asset.AssetId == Guid.Empty)
        {
            asset.AssetId = Guid.NewGuid();
        }

        var result = await _context.Assets.AddAsync(asset);
        return result.Entity.AssetId;
    }
}