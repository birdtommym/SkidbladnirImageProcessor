using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkidbladnirProcessor.App.Services;

public static class ImageStackingService
{
    public static Image<Rgba32> AverageStack(IReadOnlyList<Image<Rgba32>> images)
    {
        if (images.Count == 0)
        {
            throw new ArgumentException("Provide at least one image to stack.", nameof(images));
        }

        var width = images[0].Width;
        var height = images[0].Height;

        if (images.Any(image => image.Width != width || image.Height != height))
        {
            throw new InvalidOperationException("All frames must share the same resolution before stacking.");
        }

        var stacked = new Image<Rgba32>(width, height);
        var totals = new Vector3[width];
        var frameCount = images.Count;

        for (var y = 0; y < height; y++)
        {
            Array.Fill(totals, Vector3.Zero);

            foreach (var image in images)
            {
                var sourceRow = image.GetPixelRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var pixel = sourceRow[x];
                    totals[x] += new Vector3(pixel.R, pixel.G, pixel.B);
                }
            }

            var destinationRow = stacked.GetPixelRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                var averaged = totals[x] / frameCount;
                destinationRow[x] = new Rgba32(
                    (byte)Math.Clamp((int)MathF.Round(averaged.X), 0, 255),
                    (byte)Math.Clamp((int)MathF.Round(averaged.Y), 0, 255),
                    (byte)Math.Clamp((int)MathF.Round(averaged.Z), 0, 255),
                    255);
            }
        }

        return stacked;
    }
}
