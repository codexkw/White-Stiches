<#
.SYNOPSIS
  Localization coverage gate (Phase 1E-3). Verifies that every @L["..."] key used in the Razor
  views has an Arabic translation in the matching SharedResource.ar.resx, and heuristically counts
  remaining hardcoded English text nodes that have not been wrapped in @L.

.NOTES
  Run from anywhere:  powershell -File scripts/i18n-coverage.ps1
  Exit code is non-zero when any used key is missing its Arabic translation.
  ASCII-only on purpose (Windows PowerShell 5.1 misreads UTF-8-without-BOM script files).
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-UsedKeys([string]$viewsDir) {
    $keys = [System.Collections.Generic.HashSet[string]]::new()
    Get-ChildItem -Path $viewsDir -Recurse -Filter *.cshtml | ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        foreach ($m in [regex]::Matches($text, '@L\["([^"]*)"\]')) {
            [void]$keys.Add($m.Groups[1].Value)
        }
    }
    return $keys
}

function Get-ResxNames([string]$resxPath) {
    $names = [System.Collections.Generic.HashSet[string]]::new()
    if (Test-Path $resxPath) {
        [xml]$xml = Get-Content $resxPath -Raw -Encoding UTF8
        foreach ($d in $xml.root.data) { if ($d.name) { [void]$names.Add([string]$d.name) } }
    }
    return $names
}

function Test-App([string]$name, [string]$viewsDir, [string]$arResx) {
    Write-Host ""
    Write-Host "=== $name ===" -ForegroundColor Cyan
    $used = Get-UsedKeys $viewsDir
    $ar = Get-ResxNames $arResx
    $missing = @($used | Where-Object { -not $ar.Contains($_) } | Sort-Object)

    Write-Host ("  @L keys used in views : {0}" -f $used.Count)
    Write-Host ("  Arabic resx entries    : {0}" -f $ar.Count)
    if ($missing.Count -eq 0) {
        Write-Host "  Missing Arabic         : 0  (all used keys translated)" -ForegroundColor Green
    }
    else {
        Write-Host ("  Missing Arabic         : {0}" -f $missing.Count) -ForegroundColor Yellow
        $missing | Select-Object -First 40 | ForEach-Object { Write-Host "    - $_" }
        if ($missing.Count -gt 40) { Write-Host ("    ... and {0} more" -f ($missing.Count - 40)) }
    }
    return $missing.Count
}

function Measure-Hardcoded([string]$viewsDir) {
    $hits = 0
    Get-ChildItem -Path $viewsDir -Recurse -Filter *.cshtml | ForEach-Object {
        foreach ($line in [System.IO.File]::ReadAllLines($_.FullName)) {
            foreach ($m in [regex]::Matches($line, '>\s*([A-Za-z][A-Za-z ,&.!?-]{2,})\s*<')) {
                $t = $m.Groups[1].Value.Trim()
                if ($t -match '@') { continue }
                $hits++
            }
        }
    }
    return $hits
}

$webViews = Join-Path $root 'src/WhiteStiches.Web/Views'
$adminViews = Join-Path $root 'src/WhiteStiches.Admin/Views'
$webResx = Join-Path $root 'src/WhiteStiches.Web/Resources/SharedResource.ar.resx'
$adminResx = Join-Path $root 'src/WhiteStiches.Admin/Resources/SharedResource.ar.resx'

$missWeb = Test-App 'Storefront (Web)' $webViews $webResx
$missAdmin = Test-App 'Back office (Admin)' $adminViews $adminResx

Write-Host ""
Write-Host "=== Heuristic hardcoded-text scan (lower is better) ===" -ForegroundColor Cyan
Write-Host ("  Storefront candidate literals : {0}" -f (Measure-Hardcoded $webViews))
Write-Host ("  Admin candidate literals      : {0}" -f (Measure-Hardcoded $adminViews))
Write-Host "  (Heuristic only; long-form legal/marketing prose is intentionally left untranslated.)"
Write-Host ""

$total = $missWeb + $missAdmin
if ($total -gt 0) {
    Write-Host ("FAIL: {0} used @L keys have no Arabic translation." -f $total) -ForegroundColor Red
    exit 1
}
Write-Host "PASS: every @L key used in the views has an Arabic translation." -ForegroundColor Green
