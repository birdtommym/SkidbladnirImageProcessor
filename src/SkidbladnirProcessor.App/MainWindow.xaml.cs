using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkidbladnirProcessor.App.Processing;
using SkidbladnirProcessor.App.Services;
using SkidbladnirProcessor.App.ViewModels;

namespace SkidbladnirProcessor.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProcessedImageViewModel> _frames = new();
    private Image<Rgba32>? _stackedImageData;

    public MainWindow()
    {
        InitializeComponent();
        FramesList.ItemsSource = _frames;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp)|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Select light frames to process"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ToggleUi(false);
        try
        {
            foreach (var file in dialog.FileNames)
            {
                StatusText.Text = $"Processing {Path.GetFileName(file)}";
                var processed = await Task.Run(() => AstroImageProcessor.Process(file));
                _frames.Add(processed);
            }

            StatusText.Text = $"Loaded {_frames.Count} frame(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to process images. {ex.Message}", "Processing error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Processing failed.";
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void FramesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FramesList.SelectedItem is ProcessedImageViewModel selected)
        {
            PreviewImage.Source = selected.Preview;
            StatusText.Text = $"Previewing {selected.FileName}";
        }
        else
        {
            PreviewImage.Source = null;
        }
    }

    private async void StackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0)
        {
            MessageBox.Show(this, "Add at least one processed frame before stacking.", "Nothing to stack", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ToggleUi(false);
        try
        {
            StatusText.Text = "Stacking frames...";
            _stackedImageData?.Dispose();

            var imagesToStack = _frames.Select(frame => frame.ImageData).ToList();
            _stackedImageData = await Task.Run(() =>
            {
                var stacked = ImageStackingService.AverageStack(imagesToStack);
                AstroImageProcessor.RefineStackedImage(stacked);
                return stacked;
            });

            StackedImage.Source = BitmapSourceConverter.FromImage(_stackedImageData);
            StatusText.Text = "Stacking completed.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to stack frames. {ex.Message}", "Stacking error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Stacking failed.";
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _frames.Clear();
        PreviewImage.Source = null;
        StackedImage.Source = null;
        _stackedImageData?.Dispose();
        _stackedImageData = null;
        StatusText.Text = "Cleared all frames.";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_stackedImageData is null)
        {
            MessageBox.Show(this, "Stack the images before saving the combined frame.", "No stacked image", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save stacked image",
            Filter = "TIFF image (*.tif)|*.tif|PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
            FileName = "StackedResult.tif"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var stream = File.Create(dialog.FileName);
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            switch (extension)
            {
                case ".png":
                    _stackedImageData.SaveAsPng(stream);
                    break;
                case ".jpg":
                case ".jpeg":
                    _stackedImageData.SaveAsJpeg(stream);
                    break;
                default:
                    _stackedImageData.SaveAsTiff(stream);
                    break;
            }

            StatusText.Text = $"Saved {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to save the stacked image. {ex.Message}", "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Saving failed.";
        }
    }

    private void ToggleUi(bool isEnabled)
    {
        LoadButton.IsEnabled = isEnabled;
        ClearButton.IsEnabled = isEnabled;
        StackButton.IsEnabled = isEnabled;
        SaveButton.IsEnabled = isEnabled;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _stackedImageData?.Dispose();
    }
}
