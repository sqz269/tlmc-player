using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;

namespace TlmcPlayerBackend.Controllers.MusicData;

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

        FileStream fileStream = new FileStream(asset.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        string mime = asset.Mime ?? "application/octet-stream";

        return File(fileStream, mime, asset.Name, enableRangeProcessing: asset.Size > 1000000);
    }
}