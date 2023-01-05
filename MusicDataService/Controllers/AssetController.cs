using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data.Api;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

[ApiController]
[Route("api/asset")]
public class AssetController : Controller
{
    private readonly IAssetRepo _assetRepo;

    public AssetController(IAssetRepo assetRepo)
    {
        _assetRepo = assetRepo;
    }

    [HttpGet("{id:Guid}", Name = nameof(GetAsset))]
    public async Task<IActionResult> GetAsset(Guid id)
    {
        var asset = await _assetRepo.GetAssetById(id);
        if (asset == null)
            return NotFound();

        FileStream fileStream = new FileStream(asset.AssetPath, FileMode.Open);

        string mime = asset.AssetMime ?? "application/octet-stream";

        return File(fileStream, asset.AssetMime, mime, enableRangeProcessing: asset.Large);
    }
}