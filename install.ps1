[CmdletBinding()]
param(
    [string]$SourceExe,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\HeicToClipboard')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Get-Process HeicToClipboard -ErrorAction SilentlyContinue | Stop-Process -Force

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultBuiltExe = Join-Path $repoRoot 'artifacts\publish\win-x64\HeicToClipboard.exe'
$installedExe = Join-Path $InstallDir 'HeicToClipboard.exe'

function Resolve-SourceExe {
    param(
        [string]$ExplicitPath,
        [string]$BuiltPath,
        [string]$InstalledPath
    )

    if ($ExplicitPath) {
        $resolved = Resolve-Path -Path $ExplicitPath -ErrorAction Stop
        return $resolved.Path
    }

    if (Test-Path $BuiltPath) {
        return (Resolve-Path -Path $BuiltPath -ErrorAction Stop).Path
    }

    if (Test-Path $InstalledPath) {
        return (Resolve-Path -Path $InstalledPath -ErrorAction Stop).Path
    }

    throw "HeicToClipboard.exe was not found in the install directory. Build the project first with .\build.ps1 or pass -SourceExe <path>."
}

$sourceExePath = Resolve-SourceExe -ExplicitPath $SourceExe -BuiltPath $defaultBuiltExe -InstalledPath $installedExe

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

if ($sourceExePath -ne $installedExe) {
    Copy-Item -Path $sourceExePath -Destination $installedExe -Force
}

$installedFile = Get-Item -Path $installedExe -ErrorAction Stop

$registryTargets = @(
    'HKCU:\Software\Classes\SystemFileAssociations\.heic\shell\CandCToJpeg',
    'HKCU:\Software\Classes\SystemFileAssociations\.heif\shell\CandCToJpeg'
)

$commandValue = ('"{0}" "%1"' -f $installedExe)

foreach ($registryPath in $registryTargets) {
    $commandPath = Join-Path $registryPath 'command'

    New-Item -Path $registryPath -Force | Out-Null
    New-Item -Path $commandPath -Force | Out-Null

    Set-Item -Path $registryPath -Value 'C&C to JPEG'
    Set-ItemProperty -Path $registryPath -Name 'Icon' -Value $installedExe
    Set-ItemProperty -Path $registryPath -Name 'MultiSelectModel' -Value 'Player'
    Set-Item -Path $commandPath -Value $commandValue
}

Write-Host "Installed HeicToClipboard to $installedExe"
Write-Host ("Installed EXE timestamp: {0:yyyy-MM-dd HH:mm:ss}" -f $installedFile.LastWriteTime)
Write-Host 'Explorer context menu registered for .heic and .heif'
