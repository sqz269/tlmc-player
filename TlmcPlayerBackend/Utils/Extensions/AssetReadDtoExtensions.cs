using TlmcPlayerBackend.Dtos.MusicData.Asset;

namespace TlmcPlayerBackend.Utils.Extensions;

public static class AssetReadDtoExtensions
{
    public static void MapAssetUrl(this AssetReadDto asset, Func<Guid, string?> assetUrlGenerator)
    {
        var assetUrl = assetUrlGenerator.Invoke(asset.AssetId);
        if (assetUrl == null)
        {
            throw new InvalidOperationException("Failed to Generate Asset URI. AssetUrlGenerator call returned null");
        }
        asset.Url = assetUrl;
    }

    public static void MapAssetUrl(this List<AssetReadDto> assets, Func<Guid, string?> assetUrlGenerator)
    {
        assets.ForEach(a => a.MapAssetUrl(assetUrlGenerator));
    }
}