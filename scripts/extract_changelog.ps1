# extract_changelog.ps1
# Extrae una secciÃ³n de versiÃ³n especÃ­fica de CHANGELOG.md y la vuelca en stdout.
# Uso: .\scripts\extract_changelog.ps1 v1.0.0
#      .\scripts\extract_changelog.ps1 Unreleased

param (
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Ruta relativa al directorio de docs/CHANGELOG.md
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetPath = Join-Path $scriptDir "..\docs\CHANGELOG.md"

if (-not (Test-Path $targetPath)) {
    Write-Error "CHANGELOG.md no encontrado en: $targetPath"
    exit 1
}

# Normalizar la versiÃ³n y determinar el encabezado
if ($Version -ieq "Unreleased") {
    $sectionHeader = "## [Unreleased]"
} else {
    if ($Version -match '^v') {
        $sectionHeader = "## [$Version]"
    } else {
        $sectionHeader = "## [v$Version]"
    }
}

$lines = Get-Content -Path $targetPath -Encoding UTF8

$inSection = $false
$sectionLines = @()

foreach ($line in $lines) {
    if ($line.TrimEnd() -eq $sectionHeader -or $line.TrimStart().StartsWith("$sectionHeader ")) {
        $inSection = $true
        continue  # Saltar la lÃ­nea del encabezado
    }

    if ($inSection) {
        # Si encontramos el siguiente encabezado de segundo nivel (##), salimos de la secciÃ³n
        if ($line -match '^## \[') {
            break
        }
        $sectionLines += $line
    }
}

if ($sectionLines.Count -eq 0) {
    Write-Error "Seccion '$sectionHeader' no encontrada o vacia en CHANGELOG.md"
    exit 1
}

# Eliminar lÃ­neas vacÃ­as al principio y al final
$firstContent = 0
$lastContent = $sectionLines.Count - 1

while ($firstContent -le $lastContent -and [string]::IsNullOrWhiteSpace($sectionLines[$firstContent])) {
    $firstContent++
}
while ($lastContent -ge $firstContent -and [string]::IsNullOrWhiteSpace($sectionLines[$lastContent])) {
    $lastContent--
}

if ($firstContent -le $lastContent) {
    $trimmed = $sectionLines[$firstContent..$lastContent]
    $trimmed | Write-Output
} else {
    Write-Output "Sin cambios registrados."
}

