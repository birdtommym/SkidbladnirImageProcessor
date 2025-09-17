# Skidbladnir Image Processor

Skidbladnir Image Processor is a lightweight Windows desktop application for automating the first steps of deep sky image processing.  It automatically pre-processes every light frame that you load and then combines the frames with average stacking to produce a clean master image.

## Features

- Load multiple TIFF, PNG, JPEG or BMP light frames in one step.
- Automatic background neutralisation, histogram stretching, gamma correction, light noise reduction and saturation boost for every frame.
- Instant preview of the processed single frame.
- Average stacking of all processed frames with additional refinement.
- Save the stacked result as TIFF, PNG or JPEG.

## Project structure

```
SkidbladnirProcessor.sln               # Visual Studio solution
src/SkidbladnirProcessor.App/          # WPF application
```

The application is built with .NET 7, WPF and [ImageSharp](https://github.com/SixLabors/ImageSharp) for the processing pipeline.

## Getting started

1. Install the [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) on a Windows machine.
2. Clone the repository and open a Developer Command Prompt.
3. Restore the dependencies and build the application:

   ```bash
   dotnet restore
   dotnet build SkidbladnirProcessor.sln
   ```

4. Run the application:

   ```bash
   dotnet run --project src/SkidbladnirProcessor.App/SkidbladnirProcessor.App.csproj
   ```

   or open the solution in Visual Studio and press <kbd>F5</kbd>.

## Usage tips

1. Click **Load Light Frames** and select the light frames that you want to process.
2. Inspect the automatically processed preview for every frame from the list on the left.
3. When you are satisfied click **Stack Images** to build an averaged master frame.
4. Use **Save Stacked Result** to export the combined image.

> **Note:** The repository is prepared on a Linux build host where Windows-specific workloads cannot be executed.  Building the solution locally on Windows is therefore required.
