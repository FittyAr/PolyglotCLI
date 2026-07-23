# scripts/build_nsis_installer.ps1
# Wrapper que invoca makensis (Nullsoft Install System 3) para compilar el
# instalador .exe de PolyglotCLI equivalente al generado por Inno Setup
# (installer/PolyglotCLI.iss).
#
# Reutiliza los artefactos publicados en artifacts/publish_out y
# artifacts/publish_maui, y emite el instalador final en
# artifacts/dist/PolyglotCLI-<version>-x64-nsis-setup.exe.

param (
    [Parameter(Mandatory = $false)]
    [string]$Version = "",

    [Parameter(Mandatory = $false)]
    [switch]$NoPublish
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

# Ir a la raiz del repositorio
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
Set-Location $repoRoot

# 1. Publicar las apps si no se indico lo contrario
if (-not $NoPublish) {
    Write-Host "[1/3] Publicando PolyglotCLI.web -> artifacts\publish_out..." -ForegroundColor Yellow
    dotnet publish PolyglotCLI.web/PolyglotCLI.web.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish_out | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fallo la publicacion de PolyglotCLI.web."
        exit 1
    }

    Write-Host "[2/3] Publicando PolyglotCLI.Maui -> artifacts\publish_maui..." -ForegroundColor Yellow
    dotnet publish PolyglotCLI.Maui/PolyglotCLI.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -o artifacts\publish_maui | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fallo la publicacion de PolyglotCLI.Maui."
        exit 1
    }
} else {
    Write-Host "[1,2/3] Publicacion omitida (-NoPublish)." -ForegroundColor DarkYellow
}

# 2. Localizar makensis (NSIS 3)
$makensisCandidates = @(
    (Join-Path $env:ProgramFiles "NSIS\makensis.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "NSIS\makensis.exe"),
    "C:\Program Files\NSIS\makensis.exe",
    "C:\Program Files (x86)\NSIS\makensis.exe"
)
$makensis = $null
foreach ($candidate in $makensisCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $makensis = $candidate
        break
    }
}

if (-not $makensis) {
    Write-Error "No se encontro makensis.exe (NSIS 3). Instale NSIS desde https://nsis.sourceforge.io/Download"
    exit 1
}

# 3. Determinar la version a usar
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csproj = "PolyglotCLI.web/PolyglotCLI.web.csproj"
    $csprojContent = Get-Content -Raw -Path $csproj
    if ($csprojContent -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1]
        Write-Host "Version leida del .csproj: $Version" -ForegroundColor Cyan
    } else {
        Write-Error "No se encontro la etiqueta <Version> en $csproj y no se especifico -Version."
        exit 1
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Formato de version no valido (debe ser X.Y.Z): $Version"
    exit 1
}

# 4. Validar que los artefactos publicados existan antes de compilar.
if (-not (Test-Path "artifacts/publish_out")) {
    Write-Error "La carpeta 'artifacts\publish_out' no existe. Ejecute primero:" `
        "`n  dotnet publish PolyglotCLI.web/PolyglotCLI.web.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish_out" `
        "`no vuelva a ejecutar este script SIN el parametro -NoPublish."
    exit 1
}
if (-not (Test-Path "artifacts/publish_maui")) {
    Write-Error "La carpeta 'artifacts\publish_maui' no existe. Ejecute primero:" `
        "`n  dotnet publish PolyglotCLI.Maui/PolyglotCLI.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -o artifacts\publish_maui" `
        "`no vuelva a ejecutar este script SIN el parametro -NoPublish."
    exit 1
}

# 5. Calcular el tamano total (KB) de los artefactos publicados. Cada seccion
# de NSIS lo anade con AddSize, por lo que calculamos la mitad del tamano
# combinado para mantener un valor similar al informado por Inno Setup.
$totalKB = 0
foreach ($dir in @("artifacts/publish_out", "artifacts/publish_maui")) {
    $sum = (Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
    if ($sum) { $totalKB += [math]::Ceiling($sum / 1024) }
}
$extraSpaceKB = [math]::Ceiling($totalKB / 2)
Write-Host ("  Espacio total publicado: {0:N0} MB ({1:N0} KB), valor AddSize por seccion: {2:N0} KB" -f ($totalKB / 1024), $totalKB, $extraSpaceKB) -ForegroundColor Cyan

# 6. Compilar el .nsi con makensis
Write-Host "[3/3] Compilando instalador con makensis (version $Version)..." -ForegroundColor Yellow
& $makensis /DAPP_VERSION=$Version /DEXTRA_SPACE_KB=$extraSpaceKB /V2 installer/PolyglotCLI.nsi
if ($LASTEXITCODE -ne 0) {
    Write-Error "makensis fallo con codigo $LASTEXITCODE."
    exit 1
}

$installerPath = Resolve-Path "artifacts/dist/PolyglotCLI-$Version-x64-nsis-setup.exe" -ErrorAction SilentlyContinue
if ($installerPath) {
    Write-Host "Instalador NSIS generado: $($installerPath.Path)" -ForegroundColor Green
} else {
    Write-Host "Instalador NSIS generado en artifacts/dist/." -ForegroundColor Green
}

exit 0