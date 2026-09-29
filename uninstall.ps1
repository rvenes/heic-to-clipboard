[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$RemoveInstalledFiles,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\HeicToClipboard')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Validate everything before changing files or Explorer registration. A custom
# InstallDir is never permission to recursively delete that directory.
$resolvedInstallDir = [IO.Path]::GetFullPath($InstallDir)
$installedExe = Join-Path $resolvedInstallDir 'HeicToClipboard.exe'
$filesToRemove = @()
if ($RemoveInstalledFiles -and (Test-Path -LiteralPath $resolvedInstallDir)) {
    $directory = Get-Item -LiteralPath $resolvedInstallDir -Force
    if (-not $directory.PSIsContainer -or
        ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $directory.FullName.TrimEnd('\') -eq [IO.Path]::GetPathRoot($directory.FullName).TrimEnd('\')) {
        throw 'InstallDir must be an application directory, not a drive root, file or directory link.'
    }

    $executable = Get-Item -LiteralPath $installedExe -Force -ErrorAction Stop
    if ($executable.PSIsContainer -or
        ($executable.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $executable.VersionInfo.ProductName -ne 'HeicToClipboard' -or
        $executable.VersionInfo.OriginalFilename -ne 'HeicToClipboard.dll') {
        throw 'InstallDir does not contain a recognized HeicToClipboard installation. No files were removed.'
    }

    $filesToRemove += $executable.FullName
    $backupPath = $installedExe + '.old'
    if (Test-Path -LiteralPath $backupPath) {
        $backup = Get-Item -LiteralPath $backupPath -Force
        if (-not $backup.PSIsContainer -and
            -not ($backup.Attributes -band [IO.FileAttributes]::ReparsePoint) -and
            $backup.VersionInfo.ProductName -eq 'HeicToClipboard' -and
            $backup.VersionInfo.OriginalFilename -eq 'HeicToClipboard.dll') {
            $filesToRemove += $backup.FullName
        }
    }
}

$registryTargets = @(
    'HKCU:\Software\Classes\SystemFileAssociations\.heic\shell\CandCToJpeg',
    'HKCU:\Software\Classes\SystemFileAssociations\.heif\shell\CandCToJpeg'
)

foreach ($registryPath in $registryTargets) {
    $commandPath = Join-Path $registryPath 'command'
    if (Test-Path -LiteralPath $commandPath) {
        $registeredCommand = (Get-Item -LiteralPath $commandPath).GetValue('')
        if ($registeredCommand -eq ('"{0}" "%1"' -f $installedExe) -and
            $PSCmdlet.ShouldProcess($registryPath, 'Remove HeicToClipboard context menu')) {
            Remove-Item -LiteralPath $registryPath -Recurse -Force
        }
    }
}

foreach ($file in $filesToRemove) {
    if ($PSCmdlet.ShouldProcess($file, 'Remove HeicToClipboard executable')) {
        Remove-Item -LiteralPath $file -Force
    }
}

if ($RemoveInstalledFiles -and (Test-Path -LiteralPath $resolvedInstallDir) -and
    -not (Get-ChildItem -LiteralPath $resolvedInstallDir -Force | Select-Object -First 1)) {
    if ($PSCmdlet.ShouldProcess($resolvedInstallDir, 'Remove empty installation directory')) {
        [IO.Directory]::Delete($resolvedInstallDir, $false)
    }
}

Write-Host 'Finished processing matching Explorer context menu entries.'
if ($RemoveInstalledFiles) {
    Write-Host 'Only recognized app executables are removed. Settings and all other files are preserved.'
}
