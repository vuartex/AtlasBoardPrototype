$ErrorActionPreference = "Stop"

$ProjectRoot =
    Split-Path -Parent $PSScriptRoot

$ProjectRoot =
    [System.IO.Path]::GetFullPath(
        $ProjectRoot)

$EditorRoot =
    Join-Path $ProjectRoot "Assets\Project\Editor"

if (-not (Test-Path $EditorRoot)) {
    Write-Host "ERROR: Assets\Project\Editor not found." -ForegroundColor Red
    exit 1
}

$gitDir =
    Join-Path $ProjectRoot ".git"

if (-not (Test-Path $gitDir)) {
    Write-Host "ERROR: This project root does not contain .git" -ForegroundColor Red
    Write-Host $ProjectRoot -ForegroundColor Red
    exit 2
}

$timestamp =
    Get-Date -Format "yyyyMMdd_HHmmss"

$backupRoot =
    Join-Path $ProjectRoot (
        "Library\AtlasBoardEditorRecoveryBackup_" +
        $timestamp
    )

Write-Host ""
Write-Host "Creating safety backup..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
Copy-Item -Path $EditorRoot -Destination $backupRoot -Recurse -Force

Push-Location $ProjectRoot

try {
    $head =
        (& git rev-parse HEAD).Trim()

    if ($LASTEXITCODE -ne 0) {
        throw "git rev-parse HEAD failed."
    }

    Write-Host ("Git HEAD: " + $head) -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Restoring ONLY tracked Assets/Project/Editor files from HEAD..." -ForegroundColor Yellow

    & git restore --source=HEAD --worktree -- "Assets/Project/Editor"

    if ($LASTEXITCODE -ne 0) {
        throw "git restore failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "Tracked Editor files restored." -ForegroundColor Green
    Write-Host "Untracked Phase 14 files were preserved by git restore." -ForegroundColor Green

    $status =
        & git status --short -- "Assets/Project/Editor"

    Write-Host ""
    Write-Host "Current Editor-folder git status:" -ForegroundColor Cyan

    if ([string]::IsNullOrWhiteSpace(($status -join "`n"))) {
        Write-Host "(clean tracked Editor folder)"
    } else {
        $status | ForEach-Object {
            Write-Host $_
        }
    }

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "EDITOR RECOVERY PASS." -ForegroundColor Green
    Write-Host ("Safety backup: " + $backupRoot)
    Write-Host "Return to Unity and wait for compilation." -ForegroundColor Green
    Write-Host "DO NOT apply/restore the menu taxonomy until Unity compiles cleanly." -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
