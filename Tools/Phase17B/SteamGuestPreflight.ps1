# AtlasBoard Phase 17B.2 - read-only by default. Use -FixDevAppId to write
# a missing development-only steam_appid.txt (480) next to Guest EXE.
[CmdletBinding()]
param([switch]$FixDevAppId)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$parent = Split-Path $root -Parent
$buildFolder = Join-Path $parent 'AtlasBoard_3D3C_GuestBuild'
$exe = Join-Path $buildFolder 'AtlasBoardGuest.exe'
$appIdFile = Join-Path $buildFolder 'steam_appid.txt'
$source = Join-Path $root 'Assets\Project\Scripts\Platform\AtlasBoardSteamGuestOverlayQa.cs'
Write-Host 'AtlasBoard Phase 17B.2 Steam Guest preflight (development only)'
Write-Host "Project root: $root"
Write-Host "Build folder: $buildFolder"
Write-Host "EXE found: $(Test-Path -LiteralPath $exe)"
Write-Host "V2 QA source found: $(Test-Path -LiteralPath $source)"
$steamRunning = @(Get-Process -Name steam -ErrorAction SilentlyContinue).Count -gt 0
Write-Host "Steam desktop process running: $steamRunning"
if (!(Test-Path -LiteralPath $exe)) { throw 'Missing Guest EXE. Build Two-Client Guest Test v1 first.' }
if (!(Test-Path -LiteralPath $source)) { throw 'V2 QA source missing. Extract ZIP to AtlasBoardPrototype root.' }
$appIdState = 'MISSING'
if (Test-Path -LiteralPath $appIdFile) {
    $appIdState = ([System.IO.File]::ReadAllText($appIdFile)).Trim()
    if ($appIdState -ne '480') { throw 'EXE steam_appid.txt exists but is NOT 480. Refusing to overwrite.' }
}
if ($appIdState -eq 'MISSING' -and $FixDevAppId) {
    [System.IO.File]::WriteAllText($appIdFile, "480`n", [System.Text.Encoding]::ASCII)
    $appIdState = '480'
    Write-Host 'WROTE missing development-only steam_appid.txt = 480 beside Guest EXE.'
}
Write-Host "EXE steam_appid.txt: $appIdState"
Write-Host "Steam shortcut START IN must be: $buildFolder"
Write-Host 'Expected launch options: -atlasLocalEmulators -atlasSteamOverlayQa -atlasDevName="Colorado Player" -screen-width 1280 -screen-height 720 -screen-fullscreen 0'
Write-Host 'IMPORTANT: Steam overlay Shift+Tab is not proof that SteamAPI.Init succeeded.'
if (!$steamRunning) { Write-Warning 'Steam desktop process not detected; sign in to Steam.' }
if ($appIdState -ne '480') {
    Write-Warning 'Missing steam_appid.txt. Re-run with -FixDevAppId to create ONLY the missing development file.'
    exit 2
}
Write-Host 'PASS: local build, QA source, and AppID 480 development file present.'
