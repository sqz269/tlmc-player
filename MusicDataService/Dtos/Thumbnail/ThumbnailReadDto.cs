using MusicDataService.Dtos.Asset;

namespace MusicDataService.Dtos.Thumbnail;

public class ThumbnailReadDto
{
    public AssetReadDto? Original { get; set; }
    // 250 x 250
    public AssetReadDto? Large { get; set; }
    // 210 x 210
    public AssetReadDto? Medium { get; set; }
    // 125 x 125
    public AssetReadDto? Small { get; set; }
    // 50 x 50
    public AssetReadDto? Tiny { get; set; }

    public List<string> Colors { get; set; }
}