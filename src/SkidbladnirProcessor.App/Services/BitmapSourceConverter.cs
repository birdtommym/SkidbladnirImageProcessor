using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkidbladnirProcessor.App.Services;

public static class BitmapSourceConverter
{
    public static BitmapSource FromImage(Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, new BmpEncoder());
        stream.Position = 0;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
