[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $repoRoot 'src\CandC.HeicClipboard\CandC.HeicClipboard.csproj'
$testProject = Join-Path $repoRoot 'tests\CandC.HeicClipboard.Tests\CandC.HeicClipboard.Tests.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'

if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

& dotnet restore $appProject
& dotnet restore $testProject

& dotnet build $appProject --configuration Release --no-restore
& dotnet build $testProject --configuration Release --no-restore

& dotnet test $testProject --configuration Release --no-build

& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:AssemblyName=HeicToClipboard `
    --output $publishDir

Write-Host "Published output: $publishDir"
