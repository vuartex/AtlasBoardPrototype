$ErrorActionPreference = "Stop"

$ProjectRoot =
    Split-Path -Parent $PSScriptRoot

$ProjectRoot =
    [System.IO.Path]::GetFullPath(
        $ProjectRoot)

$AssetsRoot =
    Join-Path $ProjectRoot "Assets\Project"

if (-not (Test-Path $AssetsRoot)) {
    Write-Host "ERROR: Assets\Project not found." -ForegroundColor Red
    exit 1
}

$timestamp =
    Get-Date -Format "yyyyMMdd_HHmmss"

$backupRoot =
    Join-Path $ProjectRoot (
        "Library\AtlasBoardUnity65WarningCleanupBackup_" +
        $timestamp
    )

New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

$filesChanged = 0
$ellipsisChanges = 0
$wordWrapChanges = 0
$findObjectsChanges = 0

function Backup-And-Write {
    param(
        [string]$FilePath,
        [string]$NewContent
    )

    $relative =
        $FilePath.Substring($ProjectRoot.Length).TrimStart('\','/')

    $backupFile =
        Join-Path $backupRoot $relative

    $backupDir =
        Split-Path -Parent $backupFile

    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

    [System.IO.File]::Copy(
        $FilePath,
        $backupFile,
        $true
    )

    [System.IO.File]::WriteAllText(
        $FilePath,
        $NewContent,
        $utf8NoBom
    )
}

$files =
    Get-ChildItem -Path $AssetsRoot -Filter *.cs -File -Recurse

foreach ($file in $files) {
    $source =
        [System.IO.File]::ReadAllText(
            $file.FullName)

    $rewritten =
        $source

    $localChanges = 0

    # 1) TextMeshPro ellipsis glyph warnings.
    # LiberationSans SDF does not contain U+2026 in the current fallback chain.
    # TMP is already falling back to Truncate at runtime, so make that explicit.
    $ellipsisCount =
        ([regex]::Matches(
            $rewritten,
            "TextOverflowModes\.Ellipsis"
        )).Count

    if ($ellipsisCount -gt 0) {
        $rewritten =
            $rewritten.Replace(
                "TextOverflowModes.Ellipsis",
                "TextOverflowModes.Truncate"
            )

        $ellipsisChanges +=
            $ellipsisCount

        $localChanges +=
            $ellipsisCount
    }

    # 2) TMP_Text.enableWordWrapping obsolete warning.
    $wordWrapPattern =
        "([A-Za-z_][A-Za-z0-9_\.]*)\.enableWordWrapping\s*=\s*false\s*;"

    $wordWrapMatches =
        [regex]::Matches(
            $rewritten,
            $wordWrapPattern
        )

    if ($wordWrapMatches.Count -gt 0) {
        $rewritten =
            [regex]::Replace(
                $rewritten,
                $wordWrapPattern,
                '$1.textWrappingMode = TextWrappingModes.NoWrap;'
            )

        $wordWrapChanges +=
            $wordWrapMatches.Count

        $localChanges +=
            $wordWrapMatches.Count
    }

    # 3) Unity 6.5 removed/deprecated FindObjectsSortMode.None overloads.
    # SortMode.None requested no ordering, so the new unsorted overload has
    # identical intent.

    $includePattern =
        "FindObjectsByType<(?<type>[^>]+)>\(\s*FindObjectsInactive\.Include\s*,\s*FindObjectsSortMode\.None\s*\)"

    $includeMatches =
        [regex]::Matches(
            $rewritten,
            $includePattern
        )

    if ($includeMatches.Count -gt 0) {
        $rewritten =
            [regex]::Replace(
                $rewritten,
                $includePattern,
                'FindObjectsByType<${type}>(FindObjectsInactive.Include)'
            )

        $findObjectsChanges +=
            $includeMatches.Count

        $localChanges +=
            $includeMatches.Count
    }

    $activePattern =
        "FindObjectsByType<(?<type>[^>]+)>\(\s*FindObjectsSortMode\.None\s*\)"

    $activeMatches =
        [regex]::Matches(
            $rewritten,
            $activePattern
        )

    if ($activeMatches.Count -gt 0) {
        $rewritten =
            [regex]::Replace(
                $rewritten,
                $activePattern,
                'FindObjectsByType<${type}>()'
            )

        $findObjectsChanges +=
            $activeMatches.Count

        $localChanges +=
            $activeMatches.Count
    }

    if ($localChanges -le 0) {
        continue
    }

    Backup-And-Write `
        -FilePath $file.FullName `
        -NewContent $rewritten

    $filesChanged++

    Write-Host (
        "UPDATED  " +
        $file.FullName.Substring($ProjectRoot.Length).TrimStart('\','/')
    ) -ForegroundColor Green
}

# Verification.
$remainingEllipsis = @()
$remainingWordWrap = @()
$remainingSortMode = @()

foreach ($file in $files) {
    $source =
        [System.IO.File]::ReadAllText(
            $file.FullName)

    if ($source.Contains(
            "TextOverflowModes.Ellipsis"))
    {
        $remainingEllipsis +=
            $file.FullName
    }

    if ([regex]::IsMatch(
            $source,
            "\.enableWordWrapping\s*="))
    {
        $remainingWordWrap +=
            $file.FullName
    }

    if ($source.Contains(
            "FindObjectsSortMode.None"))
    {
        $remainingSortMode +=
            $file.FullName
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Atlas Board Unity 6.5 Warning Cleanup" -ForegroundColor Cyan
Write-Host ("Files changed               : " + $filesChanged)
Write-Host ("Ellipsis -> Truncate        : " + $ellipsisChanges)
Write-Host ("enableWordWrapping updated  : " + $wordWrapChanges)
Write-Host ("FindObjects overload updated: " + $findObjectsChanges)
Write-Host ("Backup                      : " + $backupRoot)

if ($remainingEllipsis.Count -gt 0 -or
    $remainingWordWrap.Count -gt 0 -or
    $remainingSortMode.Count -gt 0)
{
    Write-Host ""
    Write-Host "CLEANUP INCOMPLETE." -ForegroundColor Yellow

    if ($remainingEllipsis.Count -gt 0) {
        Write-Host "Remaining TextOverflowModes.Ellipsis:" -ForegroundColor Yellow
        $remainingEllipsis | Sort-Object -Unique | ForEach-Object {
            Write-Host $_ -ForegroundColor Yellow
        }
    }

    if ($remainingWordWrap.Count -gt 0) {
        Write-Host "Remaining enableWordWrapping assignments:" -ForegroundColor Yellow
        $remainingWordWrap | Sort-Object -Unique | ForEach-Object {
            Write-Host $_ -ForegroundColor Yellow
        }
    }

    if ($remainingSortMode.Count -gt 0) {
        Write-Host "Remaining FindObjectsSortMode.None:" -ForegroundColor Yellow
        $remainingSortMode | Sort-Object -Unique | ForEach-Object {
            Write-Host $_ -ForegroundColor Yellow
        }
    }

    exit 2
}

Write-Host ""
Write-Host "WARNING CLEANUP PASS." -ForegroundColor Green
Write-Host "Return to Unity and let it recompile." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
