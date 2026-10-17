$ErrorActionPreference = "Stop"

$FirebaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $FirebaseDir

Write-Host "Deploying ONLY Realtime Database rules to atlasboard-usa..." -ForegroundColor Cyan
firebase.cmd deploy --only database --project atlasboard-usa

if ($LASTEXITCODE -ne 0) {
    throw "Firebase RTDB rules deploy failed with exit code $LASTEXITCODE."
}

Write-Host "RTDB rules deploy completed successfully." -ForegroundColor Green
