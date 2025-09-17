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

The application is built with .NET 8, WPF and [ImageSharp](https://github.com/SixLabors/ImageSharp) for the processing pipeline.

## Installation and running the app

You can now install everything with a single click on Windows 10 or Windows 11.

### Option 1 – One-click Windows installer (recommended)

1. Download or clone this repository and extract it somewhere on your machine.
2. Double-click **`Install-Skidbladnir.bat`** (or right-click ➝ *Run as administrator* if your organisation policies require it). The batch file runs the bundled PowerShell installer with execution policy bypassed so you do not have to change any global settings.
3. Wait for the console window to display the success message, then press any key to close it.

During installation the script will:

- verify you are on 64-bit Windows 10/11,
- download a private copy of the .NET 8 SDK if one is not already present,
- restore NuGet packages and publish a self-contained `win-x64` build,
- copy the compiled app to `%LOCALAPPDATA%\SkidbladnirImageProcessor` (or the directory you pass with `-InstallDir`), and
- create Start menu and desktop shortcuts (desktop can be skipped with `-NoDesktopShortcut`).

An `uninstall.ps1` script is dropped next to the installed binaries and a Start menu shortcut titled **Uninstall** is created. Run either one to remove the app and its shortcuts.

You can launch the program from the Start menu entry or by opening `SkidbladnirImageProcessor.exe` inside the install directory.

If you prefer running the PowerShell script manually, open Windows Terminal in the repository root and execute:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-Skidbladnir.ps1
```

Add `-InstallDir "D:\AstroTools\Skidbladnir"` to choose a different location or `-NoDesktopShortcut` to keep your desktop clean.

### Option 2 – Manual build (advanced)

If you would rather take control of every step:

1. Install the latest [.NET 8 SDK for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and, optionally, [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the **.NET desktop development** workload.
2. Clone or extract the repository and open a terminal in the folder.
3. Restore packages and build:

   ```powershell
   dotnet restore
   dotnet build SkidbladnirProcessor.sln -c Release
   ```

4. Run the WPF application:

   ```powershell
   dotnet run --project src/SkidbladnirProcessor.App/SkidbladnirProcessor.App.csproj
   ```

   Alternatively, open the solution in Visual Studio, set `SkidbladnirProcessor.App` as the startup project, and press <kbd>F5</kbd>.

## Usage tips

1. Click **Load Light Frames** and select the light frames that you want to process.
2. Inspect the automatically processed preview for every frame from the list on the left.
3. When you are satisfied click **Stack Images** to build an averaged master frame.
4. Use **Save Stacked Result** to export the combined image.

> **Note:** The repository is prepared on a Linux build host where Windows-specific workloads cannot be executed.  Building the solution locally on Windows is therefore required.
