# scripts/install.ps1
# Compila la Web App + MAUI Desktop, genera el instalador .exe con Inno Setup 7
# y lo ejecuta para simular la instalacion de GitHub Actions.
# Equivale al antiguo scripts/install.ps1 que trabajaba con WiX MSI.

param (
    [switch]$Uninstall = $false
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

# Permitir pasar el argumento "uninstall" de forma posicional
if ($args -contains "uninstall") { $Uninstall = $true }

$ErrorActionPreference = "Stop"

# Helper: prefiere PowerShell 7 (pwsh) y cae a Windows PowerShell 5.1 (powershell) si no esta.
function Get-PowerShellExe {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return "pwsh" }
    return "powershell"
}

# Obtener directorio del script (scripts/) y la raiz del repositorio
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "."
}

$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
Set-Location $repoRoot

# Ruta del script de Inno Setup (encargado de publicar + compilar)
$buildInstallerScript = Join-Path $scriptDir "build_installer.ps1"
if (-not (Test-Path $buildInstallerScript)) {
    Write-Error "No se encontro $buildInstallerScript"
    exit 1
}

function Resolve-InstallerPath {
    $candidates = Get-ChildItem -Path "installer/dist" -Filter "PolyglotCLI-*-x64-setup.exe" -ErrorAction SilentlyContinue
    return $candidates | Select-Object -First 1
}

if ($Uninstall) {
    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host "  Desinstalador de PolyglotCLI (Inno Setup) " -ForegroundColor Blue
    Write-Host "==========================================" -ForegroundColor Blue

    # Buscar el desinstalador de Inno Setup en la ruta por defecto de instalacion
    $installRoot = Join-Path $env:ProgramFiles "FittyAr\PolyglotCLI"
    $uninstaller = Join-Path $installRoot "unins000.exe"

    if (Test-Path $uninstaller) {
        Write-Host "Ejecutando desinstalador de Inno Setup ($uninstaller)..." -ForegroundColor Yellow
        Start-Process $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait
        Write-Host "Desinstalacion finalizada." -ForegroundColor Green
    } else {
        Write-Host "[ADVERTENCIA] No se encontro $uninstaller. Es posible que PolyglotCLI no este instalado." -ForegroundColor Yellow
        $fallback = Resolve-InstallerPath
        if ($fallback) {
            Write-Host "Reintentando con el instalador: $($fallback.FullName) /UNINSTALL" -ForegroundColor Yellow
            Start-Process $fallback.FullName -ArgumentList "/UNINSTALL /VERYSILENT /SUPPRESSMSGBOXES" -Wait
            Write-Host "Desinstalacion finalizada." -ForegroundColor Green
        } else {
            Write-Error "No hay un instalador local disponible para ejecutar la desinstalacion."
            exit 1
        }
    }
    exit 0
}

Write-Host "==========================================" -ForegroundColor Blue
Write-Host "  Instalador Local de PolyglotCLI (Inno)   " -ForegroundColor Blue
Write-Host "==========================================" -ForegroundColor Blue

# 1. Publicar + compilar el instalador con el script reutilizable
Write-Host "Publicando apps y compilando instalador (scripts/build_installer.ps1)..." -ForegroundColor Yellow
$psExe = Get-PowerShellExe
& $psExe -NoLogo -NoProfile -File $buildInstallerScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la generacion del instalador. Revise los mensajes anteriores."
    exit 1
}

# 2. Buscar el instalador generado
$installer = Resolve-InstallerPath
if (-not $installer) {
    Write-Error "No se encontro el archivo PolyglotCLI-*-x64-setup.exe en installer/dist/."
    exit 1
}

# 3. Lanzar el instalador interactivo
Write-Host ""
Write-Host "Ejecutando instalador ($($installer.Name))..." -ForegroundColor Yellow
Write-Host "Siga las instrucciones en pantalla del asistente de Inno Setup." -ForegroundColor Cyan
Write-Host ""

try {
    Start-Process $installer.FullName -Wait
    Write-Host "Proceso de instalacion finalizado correctamente." -ForegroundColor Green
} catch {
    Write-Error "Fallo la ejecucion del instalador: $_"
    exit 1
}
