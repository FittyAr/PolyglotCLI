# scripts/install-remote.ps1
# Instalador one-liner para PolyglotCLI. Pensado para ser invocado vía:
#
#   irm https://raw.githubusercontent.com/FittyAr/PolyglotCLI/main/scripts/install-remote.ps1 | iex
#
# Variables de entorno que acepta:
#   POLYGLOT_INSTALL_MODE = "server" | "desktop" | "both"
#                           (por defecto pregunta interactivamente)
#   POLYGLOT_SILENT        = "1" para instalar sin preguntas (necesita MODE)
#   POLYGLOT_INSTALL_DIR   = ruta de instalación (por defecto
#                            "$env:ProgramFiles\FittyAr\PolyglotCLI")
#
# Comportamiento:
#   1. Detecta Windows + PowerShell 5.1+ (necesario para el instalador).
#   2. Pregunta la API de GitHub por la última release pública.
#   3. Descarga el .exe de Inno Setup a %TEMP%.
#   4. Lo ejecuta con los flags de componentes correctos:
#        /COMPONENTS="server"  -> solo el servidor web
#        /COMPONENTS="desktop" -> solo la app de escritorio
#        /COMPONENTS="server,desktop" -> ambos
#      Inno Setup detecta la instalación existente (mismo AppId) y
#      actualiza en lugar de reinstalar desde cero, conservando la
#      configuración de config.json.

[CmdletBinding()]
param(
    [ValidateSet("server", "desktop", "both")]
    [string]$Mode = $env:POLYGLOT_INSTALL_MODE,

    [switch]$Silent = ($env:POLYGLOT_SILENT -eq "1")
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "Continue"

# --- Salida colorida pero compatible con cualquier host ---
function Write-Section($msg) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Blue
    Write-Host "  $msg" -ForegroundColor Blue
    Write-Host "============================================" -ForegroundColor Blue
    Write-Host ""
}

function Write-Ok($msg)    { Write-Host "[OK]  $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!]   $msg" -ForegroundColor Yellow }
function Write-Err($msg)   { Write-Host "[X]   $msg" -ForegroundColor Red }

# --- 1. Pre-chequeos del entorno ---
Write-Section "PolyglotCLI · Instalador one-liner"

if ($env:OS -ne "Windows_NT") {
    Write-Err "Este script solo funciona en Windows."
    exit 1
}

if ($PSVersionTable.PSVersion.Major -lt 5) {
    Write-Err "Necesitas PowerShell 5.1 o superior. Versión actual: $($PSVersionTable.PSVersion)"
    exit 1
}

$psVer = $PSVersionTable.PSVersion.ToString()
Write-Ok "PowerShell $psVer detectado"

# --- 2. Selección del modo de instalación ---
if (-not $Mode) {
    if ($Silent) {
        Write-Warn "POLYGLOT_SILENT=1 sin POLYGLOT_INSTALL_MODE: usando 'both' por defecto."
        $Mode = "both"
    } else {
        Write-Host "Que modulos queres instalar?" -ForegroundColor Cyan
        Write-Host "  [1] server   - Servidor Web (PolyglotCLI Web)" -ForegroundColor Gray
        Write-Host "  [2] desktop  - App de escritorio nativa (PolyglotCLI MAUI)" -ForegroundColor Gray
        Write-Host "  [3] both     - Ambos (instalacion completa)" -ForegroundColor Gray
        Write-Host ""
        do {
            $choice = Read-Host "Opcion (1/2/3)"
        } while ($choice -notin @("1", "2", "3"))
        $Mode = switch ($choice) { "1" { "server" } "2" { "desktop" } "3" { "both" } }
    }
}

$components = switch ($Mode) {
    "server"  { "server" }
    "desktop" { "desktop" }
    "both"    { "server,desktop" }
}
Write-Ok "Modo seleccionado: $Mode (componentes: $components)"

# --- 3. Pedirle a GitHub la última release ---
$repo      = "FittyAr/PolyglotCLI"
$apiUrl    = "https://api.github.com/repos/$repo/releases/latest"

Write-Section "Consultando ultima release en GitHub"

try {
    $release = Invoke-RestMethod -Uri $apiUrl -Headers @{
        "User-Agent" = "PolyglotCLI-Installer"
        "Accept"     = "application/vnd.github+json"
    } -TimeoutSec 15
}
catch {
    $status = $_.Exception.Response.StatusCode.value__
    if ($status -eq 403 -or $status -eq 429) {
        Write-Err "Rate limit de GitHub alcanzado. Vuelve a intentar en una hora."
    } else {
        Write-Err "No se pudo contactar la API de GitHub: $($_.Exception.Message)"
    }
    exit 1
}

$asset = $release.assets | Where-Object { $_.name -like "PolyglotCLI-*-x64-setup.exe" } | Select-Object -First 1
if (-not $asset) {
    Write-Err "La ultima release ($($release.tag_name)) no incluye un instalador .exe para x64."
    exit 1
}

# Defensa en profundidad: el host de descarga debe ser GitHub. Aunque el
# JSON viene de api.github.com, validamos antes de bajar.
$downloadUri = [Uri]$asset.browser_download_url
if ($downloadUri.Scheme -ne "https" -or
    -not ($downloadUri.Host -ieq "github.com" -or $downloadUri.Host -ieq "objects.githubusercontent.com")) {
    Write-Err "La URL del instalador ($($downloadUri.Host)) no es un host de GitHub permitido. Abortando."
    exit 1
}

$version      = $release.tag_name.TrimStart("v")
$downloadUrl  = $asset.browser_download_url
$installerName = $asset.name
$sizeMb       = [math]::Round($asset.size / 1MB, 1)
# Digest publicado por GitHub en el campo `digest` del asset
# (formato "sha256:<hex>"). Si no viene, abortamos: sin verificación
# de integridad no deberíamos ejecutar un binario como admin.
$expectedDigest = $asset.digest
if (-not $expectedDigest -or -not $expectedDigest.StartsWith("sha256:", [StringComparison]::OrdinalIgnoreCase)) {
    Write-Err "La release no incluye digest sha256 para el instalador. No se puede verificar la integridad. Abortando."
    exit 1
}

Write-Ok "Ultima version: $version ($sizeMb MB)"
Write-Host "    $downloadUrl" -ForegroundColor DarkGray

# --- 4. Verificar/instalar .NET 10 Desktop Runtime ---
Write-Section "Verificando .NET 10 Desktop Runtime"

$needsDotNet = $true
try {
    $runtimes = & dotnet --list-runtimes 2>$null
    if ($runtimes -match "Microsoft\.(WindowsDesktop|NETCore|AspNetCore)\.App 10\.") {
        $needsDotNet = $false
    }
} catch { }

if ($needsDotNet) {
    Write-Warn ".NET 10 Desktop Runtime no detectado."
    if (-not $Silent) {
        $ans = Read-Host "Deseas instalarlo automaticamente via winget? (S/N)"
    } else {
        $ans = "S"
    }
    if ($ans -match "^[SsYy]?$" -or $ans -eq "") {
        Write-Host "Instalando .NET 10 Desktop Runtime..." -ForegroundColor Yellow
        try {
            winget install --id Microsoft.DotNet.DesktopRuntime.10 `
                           --silent `
                           --accept-package-agreements `
                           --accept-source-agreements `
                | Out-Null
        } catch {
            Write-Warn "winget fallo, probando descarga directa..."
        }
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes -notmatch "Microsoft\.(WindowsDesktop|NETCore|AspNetCore)\.App 10\.") {
            Write-Err ".NET 10 sigue sin detectarse. Instalalo manualmente desde https://dotnet.microsoft.com/download/dotnet/10.0"
            exit 1
        }
    }
}
Write-Ok ".NET 10 OK"

# --- 5. Descargar el instalador ---
Write-Section "Descargando $installerName"

$tempDir = Join-Path $env:TEMP "PolyglotCLI-Install"
if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Path $tempDir | Out-Null }
$installerPath = Join-Path $tempDir $installerName

try {
    # -UseBasicParsing por si IE engine no esta disponible
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
}
catch {
    Write-Err "Descarga fallida: $($_.Exception.Message)"
    exit 1
}
Write-Ok "Instalador descargado: $installerPath"

# Verificar integridad contra el digest publicado por GitHub. Si no
# coincide, borrar el .exe y abortar: no tiene sentido continuar.
Write-Section "Verificando SHA-256 del instalador"
$expectedHex = $expectedDigest.Substring("sha256:".Length).ToLowerInvariant()
$actualHash  = Get-FileHash -Path $installerPath -Algorithm SHA256
$actualHex   = $actualHash.Hash.ToLowerInvariant()
if ($actualHex -ne $expectedHex) {
    Write-Err "El instalador NO paso la verificacion de integridad."
    Write-Err "  Esperado: sha256:$expectedHex"
    Write-Err "  Obtenido: sha256:$actualHex"
    Remove-Item -Path $installerPath -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Ok "SHA-256 verificado correctamente."

# --- 6. Ejecutar el instalador ---
Write-Section "Iniciando instalador de PolyglotCLI"

# Inno Setup flags:
#   /VERYSILENT    -> no muestra el wizard
#   /SP-           -> suprime la pagina "Do you want to install?"
#   /CLOSEAPPLICATIONS -> cierra PolyglotCLI si esta corriendo
#   /NORESTART     -> no fuerza reinicio automatico
#   /COMPONENTS=   -> server, desktop o ambos
#   /TASKS=desktopicon -> crea acceso directo en el escritorio
$installDir = $env:POLYGLOT_INSTALL_DIR
if (-not $installDir) {
    $installDir = Join-Path $env:ProgramFiles "FittyAr\PolyglotCLI"
}
$args = @(
    "/VERYSILENT"
    "/SP-"
    "/CLOSEAPPLICATIONS"
    "/NORESTART"
    "/COMPONENTS=$components"
    "/TASKS=desktopicon"
    "/DIR=`"$installDir`""
)

Write-Host "Comando: $installerPath $($args -join ' ')" -ForegroundColor DarkGray
Write-Host ""

$proc = Start-Process -FilePath $installerPath -ArgumentList $args -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    # Inno Setup devuelve 0/1/2 en éxito; códigos 5+ suelen ser cancelaciones
    # que ya son visibles. Si es un fallo grave lo reportamos.
    if ($proc.ExitCode -gt 2) {
        Write-Err "El instalador termino con codigo de salida $($proc.ExitCode)."
        exit $proc.ExitCode
    }
}

Write-Section "Listo"
Write-Ok "PolyglotCLI $version instalado correctamente."
Write-Host ""
Write-Host "Accesos directos disponibles:" -ForegroundColor Cyan
if ($Mode -in @("server", "both")) {
    Write-Host "  - Menu Inicio: PolyglotCLI - Server" -ForegroundColor Gray
    Write-Host "  - URL local:  http://localhost:5000" -ForegroundColor Gray
}
if ($Mode -in @("desktop", "both")) {
    Write-Host "  - Menu Inicio: PolyglotCLI - Desktop" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Para actualizar mas adelante, PolyglotCLI consultara automaticamente" -ForegroundColor DarkGray
Write-Host "la ultima version en GitHub (solo en instalaciones .exe)." -ForegroundColor DarkGray
