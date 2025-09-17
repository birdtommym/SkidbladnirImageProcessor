<#
.SYNOPSIS
    One-click installer for the Skidbladnir Image Processor Windows application.
.DESCRIPTION
    Restores and publishes a self-contained build, copies it to the chosen
    installation directory, and sets up desktop/start menu shortcuts. The script
    ensures a compatible .NET SDK is available by downloading a local copy if
    necessary, so a user only needs to run this single script.
.PARAMETER InstallDir
    Destination folder for the installed application. Defaults to the current
    user's %LOCALAPPDATA%\SkidbladnirImageProcessor.
.PARAMETER NoDesktopShortcut
    Prevents the installer from creating a desktop shortcut.
.EXAMPLE
    .\Install-Skidbladnir.ps1
    Installs the app into the default location with both Start menu and desktop
    shortcuts.
.EXAMPLE
    .\Install-Skidbladnir.ps1 -InstallDir "D:\Apps\Skidbladnir" -NoDesktopShortcut
    Installs to D:\Apps\Skidbladnir without adding a desktop shortcut.
#>

[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'SkidbladnirImageProcessor'),
    [switch]$NoDesktopShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectFile = Join-Path $repoRoot 'src/SkidbladnirProcessor.App/SkidbladnirProcessor.App.csproj'
$appName = 'Skidbladnir Image Processor'
$exeName = 'SkidbladnirImageProcessor.exe'
$dotNetExecutable = 'dotnet'
$dotNetChannel = '8.0'
$localDotNetRoot = $null
$publishDir = $null

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Assert-OperatingSystem {
    if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        throw 'This installer can only run on Windows.'
    }

    if (-not [Environment]::Is64BitOperatingSystem) {
        throw 'Skidbladnir Image Processor requires a 64-bit edition of Windows 10 or Windows 11.'
    }

    if ([Environment]::OSVersion.Version.Major -lt 10) {
        throw 'Windows 10 or later is required.'
    }
}

function Test-DotNetSdkVersion {
    param(
        [Parameter(Mandatory)][string]$CommandPath,
        [int]$MinimumMajor = 8
    )

    try {
        $sdks = & $CommandPath --list-sdks 2>$null
    } catch {
        return $false
    }

    foreach ($line in $sdks) {
        if ($line -match '^(?<version>\d+\.\d+\.\d+)') {
            $major = [int]($Matches['version'].Split('.')[0])
            if ($major -ge $MinimumMajor) {
                return $true
            }
        }
    }

    return $false
}

function Install-LocalDotNetSdk {
    param([string]$Channel)

    Write-Step "Installing .NET SDK $Channel locally"

    $script:localDotNetRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SkidbladnirImageProcessor\dotnet-sdk'
    if (-not (Test-Path $script:localDotNetRoot)) {
        New-Item -ItemType Directory -Path $script:localDotNetRoot | Out-Null
    }

    $installerPath = Join-Path ([IO.Path]::GetTempPath()) ('dotnet-install-' + [Guid]::NewGuid().ToString('N') + '.ps1')

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -UseBasicParsing -OutFile $installerPath

    try {
        & $installerPath -InstallDir $script:localDotNetRoot -Channel $Channel -Architecture 'x64' | Write-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet-install.ps1 returned exit code $LASTEXITCODE."
        }
    }
    finally {
        if (Test-Path $installerPath) {
            Remove-Item $installerPath -Force
        }
    }

    $script:dotNetExecutable = Join-Path $script:localDotNetRoot 'dotnet.exe'
    if (-not (Test-Path $script:dotNetExecutable)) {
        throw 'The .NET installer did not produce dotnet.exe.'
    }

    Set-Item -Path Env:DOTNET_ROOT -Value $script:localDotNetRoot
    Set-Item -Path Env:PATH -Value ($script:localDotNetRoot + ';' + $env:PATH)
}

function Ensure-DotNetSdk {
    param([int]$MinimumMajor = 8, [string]$Channel = '8.0')

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command -and (Test-DotNetSdkVersion -CommandPath $command.Path -MinimumMajor $MinimumMajor)) {
        Write-Step "Using existing .NET SDK at $($command.Path)"
        $script:dotNetExecutable = $command.Path
        return
    }

    Install-LocalDotNetSdk -Channel $Channel
}

function Publish-Application {
    Write-Step 'Restoring NuGet packages'
    Push-Location $repoRoot
    try {
        & $script:dotNetExecutable restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE."
        }

        Write-Step 'Publishing self-contained release build'
        $publishDir = Join-Path ([IO.Path]::GetTempPath()) ('SkidbladnirPublish_' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $publishDir | Out-Null

        $publishArgs = @(
            'publish', $projectFile,
            '--configuration', 'Release',
            '--runtime', 'win-x64',
            '--self-contained', 'true',
            '--output', $publishDir,
            '--nologo',
            '/p:PublishSingleFile=true',
            '/p:IncludeNativeLibrariesForSelfExtract=true',
            '/p:PublishReadyToRun=true'
        )

        & $script:dotNetExecutable @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    return $publishDir
}

function Copy-Installation {
    param([Parameter(Mandatory)][string]$PublishDir)

    Write-Step "Copying files to $InstallDir"

    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $InstallDir | Out-Null
    Copy-Item -Path (Join-Path $PublishDir '*') -Destination $InstallDir -Recurse -Force
}

function New-Shortcut {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$TargetPath,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [string]$IconPath
    )

    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    if (Test-Path $Path) {
        Remove-Item $Path -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($Path)
        $shortcut.TargetPath = $TargetPath
        if ($Arguments) { $shortcut.Arguments = $Arguments }
        if ($WorkingDirectory) { $shortcut.WorkingDirectory = $WorkingDirectory }
        if ($IconPath) { $shortcut.IconLocation = $IconPath }
        $shortcut.Save()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
}

function Write-Uninstaller {
    $uninstallerPath = Join-Path $InstallDir 'uninstall.ps1'
    $uninstallerContent = @"
param(
    [switch]`$Silent
)

Set-StrictMode -Version Latest
`$ErrorActionPreference = 'Stop'

`$installDir = `$PSScriptRoot
`$startMenuFolder = Join-Path ([Environment]::GetFolderPath('Programs')) '$appName'
`$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) '$appName.lnk'
`$dotnetRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SkidbladnirImageProcessor\dotnet-sdk'

function Remove-IfExists(`$path) {
    if (Test-Path `$path) {
        Remove-Item `$path -Recurse -Force
    }
}

Remove-IfExists `$desktopShortcut
Remove-IfExists `$startMenuFolder
Remove-IfExists `$installDir
Remove-IfExists `$dotnetRoot

if (-not `$Silent) {
    Write-Host '$appName has been removed.'
}
"@

    Set-Content -Path $uninstallerPath -Value $uninstallerContent -Encoding UTF8
}

function Write-Shortcuts {
    $exePath = Join-Path $InstallDir $exeName
    if (-not (Test-Path $exePath)) {
        throw "Expected executable not found: $exePath"
    }

    Write-Step 'Creating Start menu shortcut'
    $programsFolder = [Environment]::GetFolderPath('Programs')
    $startMenuFolder = Join-Path $programsFolder $appName

    if (Test-Path $startMenuFolder) {
        Remove-Item $startMenuFolder -Recurse -Force
    }

    New-Shortcut -Path (Join-Path $startMenuFolder "$appName.lnk") -TargetPath $exePath -WorkingDirectory $InstallDir -IconPath $exePath

    $uninstallScript = Join-Path $InstallDir 'uninstall.ps1'
    $uninstallArgs = "-ExecutionPolicy Bypass -File `"$uninstallScript`""
    New-Shortcut -Path (Join-Path $startMenuFolder 'Uninstall.lnk') -TargetPath 'powershell.exe' -Arguments $uninstallArgs -WorkingDirectory $InstallDir -IconPath $exePath

    if (-not $NoDesktopShortcut) {
        Write-Step 'Creating desktop shortcut'
        $desktopFolder = [Environment]::GetFolderPath('Desktop')
        New-Shortcut -Path (Join-Path $desktopFolder "$appName.lnk") -TargetPath $exePath -WorkingDirectory $InstallDir -IconPath $exePath
    }
}

try {
    Assert-OperatingSystem
    Set-Item -Path Env:DOTNET_CLI_TELEMETRY_OPTOUT -Value '1'
    Set-Item -Path Env:DOTNET_NOLOGO -Value '1'

    Ensure-DotNetSdk -MinimumMajor 8 -Channel $dotNetChannel

    $publishDir = Publish-Application
    Copy-Installation -PublishDir $publishDir
    Write-Uninstaller
    Write-Shortcuts

    Write-Host "`n$appName has been installed to:`n  $InstallDir" -ForegroundColor Green
    Write-Host 'You can launch it from the Start menu or via the desktop shortcut (if created).'
    Write-Host "To remove it later, run uninstall.ps1 in the install folder or use the Start menu shortcut."
}
catch {
    Write-Error $_
    exit 1
}
finally {
    if ($publishDir -and (Test-Path $publishDir)) {
        Remove-Item $publishDir -Recurse -Force
    }

    if ($localDotNetRoot -and (Test-Path $localDotNetRoot)) {
        Remove-Item $localDotNetRoot -Recurse -Force
    }
}
