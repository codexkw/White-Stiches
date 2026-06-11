<#
.SYNOPSIS
  Merges a JSON array of { "en": "...", "ar": "..." } translation pairs into a resx pair
  (neutral/English + Arabic), skipping keys that already exist. Used by the Phase 1E-3 sweep to
  fold the localization agents' returned strings into SharedResource.resx / SharedResource.ar.resx.

.EXAMPLE
  pwsh scripts/merge-i18n.ps1 -JsonPath web-strings.json `
       -NeutralResx src/WhiteStiches.Web/Resources/SharedResource.resx `
       -ArResx      src/WhiteStiches.Web/Resources/SharedResource.ar.resx
#>
param(
    [Parameter(Mandatory)] [string]$JsonPath,
    [Parameter(Mandatory)] [string]$NeutralResx,
    [Parameter(Mandatory)] [string]$ArResx
)

$ErrorActionPreference = 'Stop'
$items = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Merge-Resx([string]$resxPath, [bool]$useArabic) {
    [xml]$xml = Get-Content $resxPath -Raw -Encoding UTF8
    $existing = @{}
    foreach ($d in $xml.root.data) { if ($d.name) { $existing[$d.name] = $true } }

    $added = 0
    foreach ($it in $items) {
        $key = $it.en
        if ([string]::IsNullOrWhiteSpace($key)) { continue }
        if ($existing.ContainsKey($key)) { continue }

        $val = if ($useArabic) { $it.ar } else { $it.en }
        if ([string]::IsNullOrWhiteSpace($val)) { $val = $it.en }

        $data = $xml.CreateElement('data')
        $data.SetAttribute('name', $key)
        $space = $xml.CreateAttribute('xml', 'space', 'http://www.w3.org/XML/1998/namespace')
        $space.Value = 'preserve'
        [void]$data.Attributes.Append($space)
        $valEl = $xml.CreateElement('value')
        $valEl.InnerText = $val
        [void]$data.AppendChild($valEl)
        [void]$xml.root.AppendChild($data)

        $existing[$key] = $true
        $added++
    }
    $xml.Save((Resolve-Path -LiteralPath $resxPath).Path)
    return $added
}

$n = Merge-Resx $NeutralResx $false
$a = Merge-Resx $ArResx $true
Write-Host ("Added {0} English + {1} Arabic entries." -f $n, $a) -ForegroundColor Green
