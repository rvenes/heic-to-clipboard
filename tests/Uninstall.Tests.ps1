[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedExe
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$uninstaller = Join-Path $repoRoot 'uninstall.ps1'
$testRoot = Join-Path $repoRoot ('artifacts\uninstall-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-MenuCommands {
    foreach ($extension in @('.heic', '.heif')) {
        $key = "HKCU:\Software\Classes\SystemFileAssociations\$extension\shell\CandCToJpeg\command"
        if (Test-Path -LiteralPath $key) { (Get-Item -LiteralPath $key).GetValue('') }
        else { '<absent>' }
    }
}
$menusBefore = @(Get-MenuCommands)

# Reject an unrelated directory, including one with an executable-shaped filename.
$unrelated = Join-Path $testRoot 'unrelated [keep]'
New-Item -ItemType Directory -Path $unrelated | Out-Null
$unrelatedExe = Join-Path $unrelated 'HeicToClipboard.exe'
Set-Content -LiteralPath $unrelatedExe -Value 'not an application'
$rejected = $false
try { & $uninstaller -InstallDir $unrelated -RemoveInstalledFiles }
catch { $rejected = $true }
Assert-True $rejected 'An unrelated directory was accepted.'
Assert-True ((Get-Content -LiteralPath $unrelatedExe -Raw).Trim() -eq 'not an application') 'An unrelated file was changed.'

$rejected = $false
try { & $uninstaller -InstallDir ([IO.Path]::GetPathRoot($testRoot)) -RemoveInstalledFiles -WhatIf }
catch { $rejected = $true }
Assert-True $rejected 'A drive root was accepted.'

# A real executable identifies the installation, but does not authorize removing data.
$installation = Join-Path $testRoot 'installation [keep]'
New-Item -ItemType Directory -Path $installation | Out-Null
$exe = Join-Path $installation 'HeicToClipboard.exe'
Copy-Item -LiteralPath $PublishedExe -Destination $exe
Copy-Item -LiteralPath $PublishedExe -Destination ($exe + '.old')
Set-Content -LiteralPath (Join-Path $installation 'settings.json') -Value '{"MaxFileSizeMb":2}'
$photos = Join-Path $installation 'photos'
New-Item -ItemType Directory -Path $photos | Out-Null
Set-Content -LiteralPath (Join-Path $photos 'keep.txt') -Value 'keep me'

& $uninstaller -InstallDir $installation -RemoveInstalledFiles -WhatIf
Assert-True (Test-Path -LiteralPath $exe) 'WhatIf removed the executable.'
& $uninstaller -InstallDir $installation -RemoveInstalledFiles
Assert-True (-not (Test-Path -LiteralPath $exe)) 'The app executable was not removed.'
Assert-True (-not (Test-Path -LiteralPath ($exe + '.old'))) 'The recognized app backup was not removed.'
Assert-True (Test-Path -LiteralPath (Join-Path $installation 'settings.json')) 'Settings were removed.'
Assert-True ((Get-Content -LiteralPath (Join-Path $photos 'keep.txt') -Raw).Trim() -eq 'keep me') 'Unrelated nested data was removed.'

# Empty application directories may be removed, without any recursive deletion.
$emptyInstallation = Join-Path $testRoot 'empty-installation'
New-Item -ItemType Directory -Path $emptyInstallation | Out-Null
Copy-Item -LiteralPath $PublishedExe -Destination (Join-Path $emptyInstallation 'HeicToClipboard.exe')
& $uninstaller -InstallDir $emptyInstallation -RemoveInstalledFiles
Assert-True (-not (Test-Path -LiteralPath $emptyInstallation)) 'An empty installation directory was not removed.'

Assert-True ((@(Get-MenuCommands) -join "`n") -eq ($menusBefore -join "`n")) 'Tests changed an existing Explorer registration.'
Write-Host 'Uninstall safety tests passed. Settings, unrelated files and existing Explorer registrations were preserved.'
