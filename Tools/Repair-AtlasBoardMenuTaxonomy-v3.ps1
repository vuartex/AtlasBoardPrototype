param([string]$ProjectRoot = "")

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
} else {
    $ProjectRoot = $ProjectRoot.Trim().Trim('"')
}

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$AssetsRoot = Join-Path $ProjectRoot "Assets\Project"

if (-not (Test-Path $AssetsRoot)) {
    Write-Host ""
    Write-Host "ERROR: Assets\Project was not found under:" -ForegroundColor Red
    Write-Host $ProjectRoot -ForegroundColor Red
    Write-Host ""
    exit 1
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupRoot = Join-Path $ProjectRoot ("Library\AtlasBoardMenuRepairBackup_v3_" + $timestamp)
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

$attributeRegex = New-Object System.Text.RegularExpressions.Regex(
    "(?s)\[\s*MenuItem\s*\(.*?\)\]",
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
)

# MenuItem optional arguments after itemName:
#   , false
#   , true
#   , false, 470
#   , true, 470
# Preserve them while deleting the duplicated OLD Atlas Board path.
$argsRegex = New-Object System.Text.RegularExpressions.Regex(
    "(?s)(,\s*(?:true|false)(?:\s*,\s*-?\d+)?\s*)\)\]\s*$",
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$filesChanged = 0
$attributesFixed = 0
$files = Get-ChildItem -Path $AssetsRoot -Filter *.cs -File -Recurse

foreach ($file in $files) {
    $source = [System.IO.File]::ReadAllText($file.FullName)
    $localFixed = 0

    $rewritten = $attributeRegex.Replace(
        $source,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            $attribute = $match.Value

            $firstAtlas = $attribute.IndexOf(
                "Atlas Board/",
                [System.StringComparison]::Ordinal
            )

            if ($firstAtlas -lt 0) {
                return $attribute
            }

            $secondAtlas = $attribute.IndexOf(
                "Atlas Board/",
                $firstAtlas + 12,
                [System.StringComparison]::Ordinal
            )

            if ($secondAtlas -lt 0) {
                return $attribute
            }

            # The first path is the desired NEW taxonomy path.
            # The second Atlas Board/... sequence is the OLD path that the
            # buggy organizer accidentally appended in place of the closing quote.
            $prefixThroughNewPath = $attribute.Substring(0, $secondAtlas)

            $argsMatch = $argsRegex.Match($attribute)
            $optionalArgs = ""

            if ($argsMatch.Success) {
                $optionalArgs = $argsMatch.Groups[1].Value
            }

            $script:attributesFixed++
            $script:localFixed++

            return
                $prefixThroughNewPath +
                '"' +
                $optionalArgs +
                ")]"
        }
    )

    if ($localFixed -le 0) {
        continue
    }

    $relative =
        $file.FullName.Substring($ProjectRoot.Length).TrimStart('\','/')

    $backupFile = Join-Path $backupRoot $relative
    $backupDir = Split-Path -Parent $backupFile
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

    [System.IO.File]::Copy(
        $file.FullName,
        $backupFile,
        $true
    )

    [System.IO.File]::WriteAllText(
        $file.FullName,
        $rewritten,
        $utf8NoBom
    )

    $filesChanged++
    Write-Host ("FIXED  " + $relative) -ForegroundColor Green
}

# Verification 1: no MenuItem may contain Atlas Board/ twice.
$remainingDuplicates = @()

# Verification 2: all Atlas Board MenuItem attributes should contain a closing
# quote after the first Atlas Board path.
$remainingMalformed = @()

foreach ($file in $files) {
    $source = [System.IO.File]::ReadAllText($file.FullName)

    foreach ($match in $attributeRegex.Matches($source)) {
        $value = $match.Value

        $first = $value.IndexOf(
            "Atlas Board/",
            [System.StringComparison]::Ordinal
        )

        if ($first -lt 0) {
            continue
        }

        $second = $value.IndexOf(
            "Atlas Board/",
            $first + 12,
            [System.StringComparison]::Ordinal
        )

        if ($second -ge 0) {
            $remainingDuplicates += $file.FullName
            break
        }

        $closingQuote = $value.IndexOf(
            '"',
            $first,
            [System.StringComparison]::Ordinal
        )

        if ($closingQuote -lt 0) {
            $remainingMalformed += $file.FullName
            break
        }
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Atlas Board Menu Taxonomy HOTFIX v3" -ForegroundColor Cyan
Write-Host ("Project root       : " + $ProjectRoot)
Write-Host ("Files repaired     : " + $filesChanged)
Write-Host ("MenuItems repaired : " + $attributesFixed)
Write-Host ("Backup             : " + $backupRoot)

if ($remainingDuplicates.Count -gt 0 -or $remainingMalformed.Count -gt 0) {
    Write-Host ""
    Write-Host "REPAIR INCOMPLETE" -ForegroundColor Red

    if ($remainingDuplicates.Count -gt 0) {
        Write-Host "Duplicate Atlas Board paths remain in:" -ForegroundColor Red
        $remainingDuplicates | Sort-Object -Unique | ForEach-Object {
            Write-Host $_ -ForegroundColor Red
        }
    }

    if ($remainingMalformed.Count -gt 0) {
        Write-Host "Malformed MenuItem attributes remain in:" -ForegroundColor Red
        $remainingMalformed | Sort-Object -Unique | ForEach-Object {
            Write-Host $_ -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "Send this output back to ChatGPT." -ForegroundColor Yellow
    exit 2
}

Write-Host ""
Write-Host "REPAIR PASS." -ForegroundColor Green
Write-Host "No duplicated Atlas Board MenuItem paths remain." -ForegroundColor Green
Write-Host "Return to Unity and let it recompile." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
