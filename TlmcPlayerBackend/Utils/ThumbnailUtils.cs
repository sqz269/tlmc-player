using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace TlmcPlayerBackend.Utils;

class Rgba32Comparator : IEqualityComparer<Rgba32>
{
    public bool Equals(Rgba32 x, Rgba32 y)
    {
        if (x == y) return true;

        return x.A == y.A && x.B == y.B && x.G == y.G && x.R == y.R;
    }

    public int GetHashCode(Rgba32 obj)
    {
        return (int)obj.A.GetHashCode();
    }
}

public static class ThumbnailUtils
{
    public static async Task GenerateThumbImage(string src, string dst, int width)
    {
        using var image = await Image.LoadAsync(src);
        image.Mutate(i => i.Resize(new Size(width, 0)));
        await image.SaveAsJpegAsync(dst);
    }
    
    public static async Task<List<Tuple<Rgba32, int>>> CalculateNthDominantColor(string src, int maxColor=8)
    {
        using var image = await Image.LoadAsync<Rgba32>(src);

        var quantizer = new OctreeQuantizer(new QuantizerOptions() { MaxColors = maxColor });
        image.Mutate(i =>
        {
            i.Resize(new ResizeOptions { Sampler = new NearestNeighborResampler(), Size = new Size(100, 0) });
            i.Quantize(quantizer);
        });

        var counter = new Dictionary<Rgba32, int>(new Rgba32Comparator());

        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image[x, y];
                if (counter.ContainsKey(pixel))
                {
                    counter[pixel]++;
                }
                else
                {
                    counter.Add(pixel, 1);
                }
            }
        }

        var colorDistribution = new List<Tuple<Rgba32, int>>();

        foreach (var (color, distr) in counter)
        {
            colorDistribution.Add(new Tuple<Rgba32, int>(color, distr));
        }

        var sorted =
            from c in colorDistribution
            orderby c.Item2 descending
            select c;

        return sorted.ToList();
    }
}