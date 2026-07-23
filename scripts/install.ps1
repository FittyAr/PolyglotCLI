# scripts/install.ps1
# Compila la Web App, genera el instalador MSI con WiX localmente y lo ejecuta para simular la instalacion de GitHub Actions.

param (
    [switch]$Uninstall = $false
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Permitir pasar el argumento "uninstall" de forma posicional
if ($args -contains "uninstall") { $Uninstall = $true }

$ErrorActionPreference = "Stop"

# Obtener directorio del script (scripts/) y la raiz del repositorio
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "."
}

# La raiz del repositorio es el directorio padre de scripts/
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
Set-Location $repoRoot

$csprojPath = "PolyglotCLI.web/PolyglotCLI.web.csproj"
$wixprojPath = "PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj"

if ($Uninstall) {
    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host "   Desinstalador de PolyglotCLI (WiX MSI) " -ForegroundColor Blue
    Write-Host "==========================================" -ForegroundColor Blue

    # Intentar buscar el instalador MSI generado localmente
    $msiFile = Get-ChildItem -Path "PolyglotCLI.Wix/bin" -Filter "PolyglotCLI.Wix.msi" -Recurse | Select-Object -First 1
    
    if (-not $msiFile) {
        Write-Host "Compilando instalador temporal para ejecutar la desinstalacion..." -ForegroundColor Yellow
        dotnet build $wixprojPath -c Release | Out-Null
        $msiFile = Get-ChildItem -Path "PolyglotCLI.Wix/bin" -Filter "PolyglotCLI.Wix.msi" -Recurse | Select-Object -First 1
    }

    if ($msiFile) {
        Write-Host "Ejecutando desinstalador de Windows Installer..." -ForegroundColor Yellow
        Start-Process msiexec.exe -ArgumentList "/x `"$($msiFile.FullName)`"" -Wait
        Write-Host "Desinstalacion finalizada." -ForegroundColor Green
    } else {
        Write-Error "No se pudo encontrar ni generar el instalador MSI para ejecutar la desinstalacion."
        exit 1
    }
    exit 0
}

Write-Host "==========================================" -ForegroundColor Blue
Write-Host "    Instalador Local de PolyglotCLI (MSI) " -ForegroundColor Blue
Write-Host "==========================================" -ForegroundColor Blue

# 1. Compilar y publicar la aplicacion Web en publish_out
Write-Host "Publicando PolyglotCLI.web en publish_out..." -ForegroundColor Yellow
try {
    dotnet publish $csprojPath -c Release -r win-x64 --self-contained false -o publish_out | Out-Null
    Write-Host "Publicacion exitosa." -ForegroundColor Green
} catch {
    Write-Error "Fallo la publicacion de dotnet: $_"
    exit 1
}

# 2. Compilar el proyecto de WiX para generar el instalador MSI
Write-Host "Compilando proyecto WiX para generar instalador MSI..." -ForegroundColor Yellow
try {
    dotnet build $wixprojPath -c Release | Out-Null
    Write-Host "Instalador MSI compilado exitosamente." -ForegroundColor Green
} catch {
    Write-Error "Fallo la compilacion del instalador MSI de WiX: $_"
    exit 1
}

# 3. Buscar el instalador MSI generado
$msiFile = Get-ChildItem -Path "PolyglotCLI.Wix/bin" -Filter "PolyglotCLI.Wix.msi" -Recurse | Select-Object -First 1

if (-not $msiFile) {
    Write-Error "No se encontro el archivo PolyglotCLI.Wix.msi en los directorios de salida de WiX."
    exit 1
}

# 4. Lanzar el instalador MSI interactivo de forma nativa
Write-Host "Ejecutando instalador MSI ($($msiFile.Name))..." -ForegroundColor Yellow
Write-Host "Siga las instrucciones en pantalla en el asistente interactivo." -ForegroundColor Cyan
Write-Host ""

try {
    # Ejecutamos el MSI de forma interactiva. Esto cargara la UI WixUI_FeatureTree
    # y le permitira al usuario elegir carpeta, accesos directos y validar el flujo real.
    Start-Process msiexec.exe -ArgumentList "/i `"$($msiFile.FullName)`"" -Wait
    Write-Host "Proceso de instalacion finalizado correctamente." -ForegroundColor Green
} catch {
    Write-Error "Fallo la ejecucion de msiexec: $_"
    exit 1
}

