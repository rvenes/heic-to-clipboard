[CmdletBinding()]
param(
    [switch]$RemoveInstalledFiles,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\HeicToClipboard')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$registryTargets = @(
    'HKCU:\Software\Classes\SystemFileAssociations\.heic\shell\CandCToJpeg',
    'HKCU:\Software\Classes\SystemFileAssociations\.heif\shell\CandCToJpeg'
)

foreach ($registryPath in $registryTargets) {
    if (Test-Path $registryPath) {
        Remove-Item -Path $registryPath -Recurse -Force
    }
}

if ($RemoveInstalledFiles -and (Test-Path $InstallDir)) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host 'Explorer context menu entries removed.'
if ($RemoveInstalledFiles) {
    Write-Host "Removed installed files from $InstallDir"
}
