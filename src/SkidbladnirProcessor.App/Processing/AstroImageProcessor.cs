using System;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkidbladnirProcessor.App.Services;
using SkidbladnirProcessor.App.ViewModels;

namespace SkidbladnirProcessor.App.Processing;

public static class AstroImageProcessor
{
    public static ProcessedImageViewModel Process(string filePath)
    {
        var image = Image.Load<Rgba32>(filePath);
        image.Mutate(ctx => ctx.AutoOrient());

        NormalizeChannels(image);
        StretchHistogram(image);
        ApplyGamma(image, 0.85d);
        ReduceNoise(image);
        BoostSaturation(image, 1.15f);

        var preview = BitmapSourceConverter.FromImage(image);
        return new ProcessedImageViewModel(filePath, image, preview);
    }

    public static void RefineStackedImage(Image<Rgba32> stacked)
    {
        NormalizeChannels(stacked);
        StretchHistogram(stacked);
        ApplyGamma(stacked, 0.9d);
        BoostSaturation(stacked, 1.08f);
    }

    private static void NormalizeChannels(Image<Rgba32> image)
    {
        double sumR = 0;
        double sumG = 0;
        double sumB = 0;
        var pixelCount = image.Width * image.Height;

        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                var pixel = row[x];
                sumR += pixel.R;
                sumG += pixel.G;
                sumB += pixel.B;
            }
        }

        var avgR = sumR / pixelCount;
        var avgG = sumG / pixelCount;
        var avgB = sumB / pixelCount;
        var target = (avgR + avgG + avgB) / 3d;

        var scaleR = avgR > 0 ? target / avgR : 1d;
        var scaleG = avgG > 0 ? target / avgG : 1d;
        var scaleB = avgB > 0 ? target / avgB : 1d;

        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                pixel.R = ClampToByte(pixel.R * scaleR);
                pixel.G = ClampToByte(pixel.G * scaleG);
                pixel.B = ClampToByte(pixel.B * scaleB);
            }
        }
    }

    private static void StretchHistogram(Image<Rgba32> image)
    {
        Span<int> histogram = stackalloc int[256];
        var pixelCount = image.Width * image.Height;

        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                var pixel = row[x];
                var luminance = 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;
                var index = (int)Math.Clamp(Math.Round(luminance), 0, 255);
                histogram[index]++;
            }
        }

        var lowThreshold = (int)(pixelCount * 0.01);
        var highThreshold = (int)(pixelCount * 0.99);

        var cumulative = 0;
        var low = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= lowThreshold)
            {
                low = i;
                break;
            }
        }

        cumulative = 0;
        var high = 255;
        for (var i = 0; i < histogram.Length; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= highThreshold)
            {
                high = i;
                break;
            }
        }

        if (high <= low)
        {
            high = Math.Min(255, low + 1);
            low = Math.Max(0, high - 1);
        }

        var scale = 255f / Math.Max(1, high - low);

        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                pixel.R = StretchChannel(pixel.R, low, scale);
                pixel.G = StretchChannel(pixel.G, low, scale);
                pixel.B = StretchChannel(pixel.B, low, scale);
            }
        }
    }

    private static void ApplyGamma(Image<Rgba32> image, double gamma)
    {
        var inverse = 1d / gamma;

        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                pixel.R = ApplyGamma(pixel.R, inverse);
                pixel.G = ApplyGamma(pixel.G, inverse);
                pixel.B = ApplyGamma(pixel.B, inverse);
            }
        }
    }

    private static void ReduceNoise(Image<Rgba32> image)
    {
        using var clone = image.Clone();

        for (var y = 0; y < image.Height; y++)
        {
            var destinationRow = image.GetPixelRowSpan(y);
            var originalRow = clone.GetPixelRowSpan(y);
            for (var x = 0; x < destinationRow.Length; x++)
            {
                var smoothed = SmoothPixel(clone, x, y);
                var original = originalRow[x];
                var blended = new Vector3(original.R, original.G, original.B) * 0.4f + smoothed * 0.6f;
                destinationRow[x] = new Rgba32(
                    (byte)Math.Clamp((int)MathF.Round(blended.X), 0, 255),
                    (byte)Math.Clamp((int)MathF.Round(blended.Y), 0, 255),
                    (byte)Math.Clamp((int)MathF.Round(blended.Z), 0, 255),
                    255);
            }
        }
    }

    private static void BoostSaturation(Image<Rgba32> image, float factor)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                ref var pixel = ref row[x];
                var gray = (pixel.R + pixel.G + pixel.B) / 3f;
                pixel.R = ClampToByte(gray + (pixel.R - gray) * factor);
                pixel.G = ClampToByte(gray + (pixel.G - gray) * factor);
                pixel.B = ClampToByte(gray + (pixel.B - gray) * factor);
            }
        }
    }

    private static Vector3 SmoothPixel(Image<Rgba32> source, int x, int y)
    {
        var width = source.Width;
        var height = source.Height;
        var sum = Vector3.Zero;
        var count = 0;

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var sampleY = y + offsetY;
            if ((uint)sampleY >= (uint)height)
            {
                continue;
            }

            var row = source.GetPixelRowSpan(sampleY);
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var sampleX = x + offsetX;
                if ((uint)sampleX >= (uint)width)
                {
                    continue;
                }

                var pixel = row[sampleX];
                sum += new Vector3(pixel.R, pixel.G, pixel.B);
                count++;
            }
        }

        return count == 0 ? Vector3.Zero : sum / count;
    }

    private static byte StretchChannel(byte value, int blackPoint, float scale)
    {
        var adjusted = (value - blackPoint) * scale;
        return (byte)Math.Clamp((int)Math.Round(adjusted), 0, 255);
    }

    private static byte ApplyGamma(byte value, double inverseGamma)
    {
        var normalized = value / 255d;
        var corrected = Math.Pow(normalized, inverseGamma);
        return (byte)Math.Clamp((int)Math.Round(corrected * 255d), 0, 255);
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
