using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace MusicDataService.Utils;

public static class ThumbnailUtils
{
    public static async Task GenerateThumbImage(string src, string dst, int width, int height)
    {
        using var image = await Image.LoadAsync(src);
        image.Mutate(i => i.Resize(new Size(width, height)));
        await image.SaveAsJpegAsync(dst);
    }
}