# scripts/install_nsis.ps1
# Compila la Web App + MAUI Desktop, genera el instalador .exe con Nullsoft
# Install System (NSIS 3) y lo ejecuta para simular la instalacion local.
# Equivalente al scripts/install.ps1 (Inno Setup) usando la opcion 8 de run.ps1.

param (
    [switch]$Uninstall = $false
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

if ($args -contains "uninstall") { $Uninstall = $true }

$ErrorActionPreference = "Stop"

function Get-PowerShellExe {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return "pwsh" }
    return "powershell"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "."
}

$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
Set-Location $repoRoot

$buildNsisScript = Join-Path $scriptDir "build_nsis_installer.ps1"
if (-not (Test-Path $buildNsisScript)) {
    Write-Error "No se encontro $buildNsisScript"
    exit 1
}

function Resolve-NsisInstallerPath {
    $candidates = Get-ChildItem -Path "artifacts/dist" -Filter "PolyglotCLI-*-x64-nsis-setup.exe" -ErrorAction SilentlyContinue
    return $candidates | Select-Object -First 1
}

if ($Uninstall) {
    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host "  Desinstalador de PolyglotCLI (NSIS)     " -ForegroundColor Blue
    Write-Host "==========================================" -ForegroundColor Blue

    $installRoot = Join-Path $env:ProgramFiles "FittyAr\PolyglotCLI"
    $uninstaller = Join-Path $installRoot "Uninst.exe"

    if (Test-Path $uninstaller) {
        Write-Host "Ejecutando desinstalador de NSIS ($uninstaller)..." -ForegroundColor Yellow
        Start-Process $uninstaller -ArgumentList "/S" -Wait
        Write-Host "Desinstalacion finalizada." -ForegroundColor Green
    } else {
        Write-Host "[ADVERTENCIA] No se encontro $uninstaller. Es posible que PolyglotCLI no este instalado." -ForegroundColor Yellow
        Write-Host "Si la instalacion se realizo con Inno Setup, use scripts/install.ps1 uninstall." -ForegroundColor Yellow
        $fallback = Resolve-NsisInstallerPath
        if ($fallback) {
            Write-Host "Reintentando con el instalador NSIS: $($fallback.FullName)" -ForegroundColor Yellow
            Write-Host "Abra el .exe y elija 'Eliminar instalacion' para desinstalar." -ForegroundColor Yellow
        } else {
            Write-Error "No hay un instalador NSIS local disponible para ejecutar la desinstalacion."
            exit 1
        }
    }
    exit 0
}

Write-Host "==========================================" -ForegroundColor Blue
Write-Host "  Instalador Local de PolyglotCLI (NSIS)   " -ForegroundColor Blue
Write-Host "==========================================" -ForegroundColor Blue

# 1. Publicar + compilar el instalador con el script reutilizable
Write-Host "Publicando apps y compilando instalador NSIS (scripts/build_nsis_installer.ps1)..." -ForegroundColor Yellow
$psExe = Get-PowerShellExe
& $psExe -NoLogo -NoProfile -File $buildNsisScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la generacion del instalador NSIS. Revise los mensajes anteriores."
    exit 1
}

# 2. Buscar el instalador generado
$installer = Resolve-NsisInstallerPath
if (-not $installer) {
    Write-Error "No se encontro el archivo PolyglotCLI-*-x64-nsis-setup.exe en artifacts/dist/."
    exit 1
}

# 3. Lanzar el instalador interactivo
Write-Host ""
Write-Host "Ejecutando instalador NSIS ($($installer.Name))..." -ForegroundColor Yellow
Write-Host "Siga las instrucciones en pantalla del asistente de NSIS." -ForegroundColor Cyan
Write-Host ""

try {
    Start-Process $installer.FullName -Wait
    Write-Host "Proceso de instalacion NSIS finalizado correctamente." -ForegroundColor Green
} catch {
    Write-Error "Fallo la ejecucion del instalador NSIS: $_"
    exit 1
}