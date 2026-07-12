# Builds the app and stages a venes.org release (versioned exe + latest.json + site files)
# in artifacts\site. Nothing is uploaded unless -Publish is given, which copies the staged
# files to the auto-synced web folder.
[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$SkipBuild,
    [string]$SiteDir = 'H:\Koding\Venes.org\heictoclipboard'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishedExe = Join-Path $repoRoot 'artifacts\publish\win-x64\HeicToClipboard.exe'
$stageDir = Join-Path $repoRoot 'artifacts\site'

if (-not $SkipBuild) {
    & (Join-Path $repoRoot 'build.ps1')
}

if (-not (Test-Path $publishedExe)) {
    throw "Published exe not found at $publishedExe. Run .\build.ps1 first or drop -SkipBuild."
}

[xml]$project = Get-Content (Join-Path $repoRoot 'src\CandC.HeicClipboard\CandC.HeicClipboard.csproj')
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    throw 'Could not read <Version> from the csproj.'
}

$versionedExeName = "HeicToClipboard-$version.exe"

if (Test-Path $stageDir) {
    Remove-Item -Path $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

Copy-Item -Path $publishedExe -Destination (Join-Path $stageDir $versionedExeName)
Copy-Item -Path (Join-Path $repoRoot 'site\index.html') -Destination $stageDir
Copy-Item -Path (Join-Path $repoRoot 'site\install.ps1') -Destination $stageDir

$exeItem = Get-Item (Join-Path $stageDir $versionedExeName)
$sha256 = (Get-FileHash -Path $exeItem.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    version     = "$version"
    file        = $versionedExeName
    sha256      = $sha256
    size        = $exeItem.Length
    releaseDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
}
$manifestJson = ($manifest | ConvertTo-Json)
Set-Content -Path (Join-Path $stageDir 'latest.json') -Value $manifestJson -Encoding utf8

Write-Host ''
Write-Host "Staged release $version in $stageDir"
Write-Host $manifestJson

if ($Publish) {
    New-Item -ItemType Directory -Path $SiteDir -Force | Out-Null
    Copy-Item -Path (Join-Path $stageDir '*') -Destination $SiteDir -Force
    Write-Host "Published release $version to $SiteDir"
} else {
    Write-Host "Dry run: nothing copied to $SiteDir. Re-run with -Publish to release."
}
