# HeicToClipboard web installer.
# Usage:  irm https://venes.org/heictoclipboard/install.ps1 | iex
# Downloads the latest release from venes.org, verifies the SHA-256 checksum,
# installs to %LocalAppData%\Programs\HeicToClipboard and registers the
# "C&C to JPEG" Explorer context menu for .heic/.heif. No admin rights needed.

$ErrorActionPreference = 'Stop'

$baseUrl = 'https://venes.org/heictoclipboard'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\HeicToClipboard'
$installedExe = Join-Path $installDir 'HeicToClipboard.exe'

Write-Host "Fetching release info from $baseUrl/latest.json ..."
$manifest = Invoke-RestMethod -Uri "$baseUrl/latest.json"

if (-not $manifest.version -or -not $manifest.file -or -not $manifest.sha256) {
    throw 'The update manifest is missing required fields (version, file, sha256).'
}
if ($manifest.file -match '[\\/]') {
    throw 'The update manifest contains an invalid file name.'
}

$downloadPath = Join-Path ([IO.Path]::GetTempPath()) $manifest.file
Write-Host "Downloading HeicToClipboard $($manifest.version) ..."
Invoke-WebRequest -Uri "$baseUrl/$([uri]::EscapeDataString($manifest.file))" -OutFile $downloadPath

$actualHash = (Get-FileHash -Path $downloadPath -Algorithm SHA256).Hash
if ($actualHash -ne $manifest.sha256.ToUpperInvariant()) {
    Remove-Item -Path $downloadPath -Force
    throw "Checksum verification failed. Expected $($manifest.sha256), got $actualHash."
}
Write-Host 'Checksum verified.'

Get-Process HeicToClipboard -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Move-Item -Path $downloadPath -Destination $installedExe -Force

$registryTargets = @(
    'HKCU:\Software\Classes\SystemFileAssociations\.heic\shell\CandCToJpeg',
    'HKCU:\Software\Classes\SystemFileAssociations\.heif\shell\CandCToJpeg'
)

$commandValue = ('"{0}" "%1"' -f $installedExe)

foreach ($registryPath in $registryTargets) {
    $commandPath = Join-Path $registryPath 'command'

    New-Item -Path $registryPath -Force | Out-Null
    New-Item -Path $commandPath -Force | Out-Null

    # && because a single & is the menu accelerator prefix and would render as "CC to JPEG"
    Set-Item -Path $registryPath -Value 'C&&C to JPEG'
    Set-ItemProperty -Path $registryPath -Name 'Icon' -Value $installedExe
    Set-ItemProperty -Path $registryPath -Name 'MultiSelectModel' -Value 'Player'
    Set-Item -Path $commandPath -Value $commandValue
}

Write-Host "Installed HeicToClipboard $($manifest.version) to $installedExe"
Write-Host 'Explorer context menu registered for .heic and .heif'
Write-Host 'Right click a HEIC file and choose "C&C to JPEG" to get started.'
