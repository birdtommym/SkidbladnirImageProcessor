using System;
using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkidbladnirProcessor.App.ViewModels;

public sealed class ProcessedImageViewModel : IDisposable
{
    public ProcessedImageViewModel(string filePath, Image<Rgba32> imageData, BitmapSource preview)
    {
        FilePath = filePath;
        ImageData = imageData;
        Preview = preview;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public BitmapSource Preview { get; }

    public Image<Rgba32> ImageData { get; }

    public DateTime ProcessedAtUtc { get; }

    public void Dispose()
    {
        ImageData.Dispose();
    }
}
