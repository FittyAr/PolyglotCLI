# run.ps1
# Script de control de desarrollo para PolyglotCLI.

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

# Asegurar que estamos en el directorio raiz del proyecto
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "."
}
Set-Location $scriptDir

# Flujo del menu principal del script
function Show-MainMenu {
    while ($true) {
        Clear-Host
        Write-Host "==========================================" -ForegroundColor Blue
        Write-Host "      Consola de Desarrollo PolyglotCLI   " -ForegroundColor Blue
        Write-Host "==========================================" -ForegroundColor Blue
        Write-Host " 1. Ejecutar en Modo Desktop (Ventana)" -ForegroundColor White
        Write-Host " 2. Ejecutar en Modo Web (Servidor / Navegador)" -ForegroundColor White
        Write-Host " 3. Ejecutar Pruebas Unitarias (dotnet test)" -ForegroundColor White
        Write-Host " 4. Compilar Proyecto (dotnet build)" -ForegroundColor White
        Write-Host " 5. Limpiar Compilaciones (dotnet clean)" -ForegroundColor White
        Write-Host " 6. Versionar y Publicar Release (Git Release Pipeline)" -ForegroundColor Cyan
        Write-Host " 7. Instalar/Desinstalar Localmente (ejecutar instalador)" -ForegroundColor White
        Write-Host " 0. Salir" -ForegroundColor White
        Write-Host "==========================================" -ForegroundColor Blue
        
        $opt = Read-Host "Seleccione una opcion [0-7]"
        
        switch ($opt) {
            "1" {
                Write-Host "[INFO] Iniciando PolyglotCLI.Maui en modo Desktop..." -ForegroundColor Yellow
                dotnet run --project PolyglotCLI.Maui -f net10.0-windows10.0.19041.0
                Read-Host "Presione Enter para continuar..."
            }
            "2" {
                Write-Host "[INFO] Iniciando PolyglotCLI.web en modo Web..." -ForegroundColor Yellow
                dotnet run --project PolyglotCLI.web
                Read-Host "Presione Enter para continuar..."
            }
            "3" {
                Write-Host "[INFO] Ejecutando pruebas unitarias..." -ForegroundColor Yellow
                dotnet test
                Read-Host "Presione Enter para continuar..."
            }
            "4" {
                Write-Host "[INFO] Compilando solucion..." -ForegroundColor Yellow
                dotnet build
                Read-Host "Presione Enter para continuar..."
            }
            "5" {
                Write-Host "[INFO] Limpiando artefactos de compilacion..." -ForegroundColor Yellow
                dotnet clean
                Read-Host "Presione Enter para continuar..."
            }
            "6" {
                Write-Host "[INFO] Iniciando pipeline de release..." -ForegroundColor Yellow
                powershell -ExecutionPolicy Bypass -File scripts/bump_version.ps1
                Read-Host "Presione Enter para continuar..."
            }
            "7" {
                Write-Host "[INFO] Iniciando instalador..." -ForegroundColor Yellow
                powershell -ExecutionPolicy Bypass -File scripts/install.ps1
                Read-Host "Presione Enter para continuar..."
            }
            "0" {
                Write-Host "Saliendo..." -ForegroundColor Yellow
                return
            }
            default {
                Write-Host "[ADVERTENCIA] Opcion no valida." -ForegroundColor Yellow
                Start-Sleep -Seconds 1
            }
        }
    }
}

# Ejecutar el menu principal
Show-MainMenu

