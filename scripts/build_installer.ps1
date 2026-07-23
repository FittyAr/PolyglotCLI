# scripts/build_installer.ps1
# Wrapper que invoca ISCC (Inno Setup 7) para compilar el instalador .exe de PolyglotCLI.
# Reemplaza la generacion del MSI que antes producia el proyecto WiX.

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

# 2. Localizar ISCC (Inno Setup 7)
$isccCandidates = @(
    (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe"
)
$iscc = $null
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $iscc = $candidate
        break
    }
}

if (-not $iscc) {
    Write-Error "No se encontro ISCC.exe (Inno Setup 7). Instale Inno Setup 7 desde https://jrsoftware.org/isinfo.php"
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

# 4. Compilar el .iss con ISCC
Write-Host "[3/3] Compilando instalador con ISCC (version $Version)..." -ForegroundColor Yellow

# Validar que los artefactos publicados existan antes de compilar.
# (Esto NO se valida dentro del .iss porque el instalador final empaqueta los
# archivos en su interior y los usuarios que lo descargan desde GitHub / winget
# no necesitan estas carpetas en su equipo.)
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

# Calcular el tamano total (KB) de los artefactos publicados para informar a
# Inno Setup del espacio minimo necesario en la pantalla de seleccion de
# carpeta de destino. Las directivas [Files] usan Components:, por lo que
# Inno Setup NO las contabiliza automaticamente en ExtraDiskSpaceRequired.
$extraSpaceKB = 0
foreach ($dir in @("artifacts/publish_out", "artifacts/publish_maui")) {
    $sum = (Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
    if ($sum) { $extraSpaceKB += [math]::Ceiling($sum / 1024) }
}
Write-Host ("  Espacio total publicado: {0:N0} MB ({1:N0} KB)" -f ($extraSpaceKB / 1024), $extraSpaceKB) -ForegroundColor Cyan

& $iscc /DAPP_VERSION=$Version /DEXTRA_SPACE_KB=$extraSpaceKB /Q installer/PolyglotCLI.iss
if ($LASTEXITCODE -ne 0) {
    Write-Error "ISCC fallo con codigo $LASTEXITCODE."
    exit 1
}

$installerPath = Resolve-Path "artifacts/dist/PolyglotCLI-$Version-x64-setup.exe" -ErrorAction SilentlyContinue
if ($installerPath) {
    Write-Host "Instalador generado: $($installerPath.Path)" -ForegroundColor Green
} else {
    Write-Host "Instalador generado en artifacts/dist/." -ForegroundColor Green
}

exit 0
