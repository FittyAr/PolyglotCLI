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
        Write-Host " 1. Ejecutar la Web App (dotnet run)" -ForegroundColor White
        Write-Host " 2. Ejecutar Pruebas Unitarias (dotnet test)" -ForegroundColor White
        Write-Host " 3. Compilar Proyecto (dotnet build)" -ForegroundColor White
        Write-Host " 4. Limpiar Compilaciones (dotnet clean)" -ForegroundColor White
        Write-Host " 5. Versionar y Publicar Release (Git Release Pipeline)" -ForegroundColor Cyan
        Write-Host " 0. Salir" -ForegroundColor White
        Write-Host "==========================================" -ForegroundColor Blue
        
        $opt = Read-Host "Seleccione una opcion [0-5]"
        
        switch ($opt) {
            "1" {
                Write-Host "[INFO] Iniciando PolyglotCLI.web..." -ForegroundColor Yellow
                dotnet run --project PolyglotCLI.web
                Read-Host "Presione Enter para continuar..."
            }
            "2" {
                Write-Host "[INFO] Ejecutando pruebas unitarias..." -ForegroundColor Yellow
                dotnet test
                Read-Host "Presione Enter para continuar..."
            }
            "3" {
                Write-Host "[INFO] Compilando solucion..." -ForegroundColor Yellow
                dotnet build
                Read-Host "Presione Enter para continuar..."
            }
            "4" {
                Write-Host "[INFO] Limpiando artefactos de compilacion..." -ForegroundColor Yellow
                dotnet clean
                Read-Host "Presione Enter para continuar..."
            }
            "5" {
                Write-Host "[INFO] Iniciando pipeline de release..." -ForegroundColor Yellow
                powershell -ExecutionPolicy Bypass -File scripts/bump_version.ps1
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

