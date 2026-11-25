# scripts/install-remote.ps1
# Instalador one-liner para PolyglotCLI. Pensado para ser invocado vía:
#
#   irm https://raw.githubusercontent.com/FittyAr/PolyglotCLI/master/scripts/install-remote.ps1 | iex
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
#
# Importante: como corre dentro de la sesión del usuario (irm | iex), el
# script NUNCA usa `exit` (cerraría la consola del usuario). Toda la
# lógica vive en Invoke-PolyglotInstall y los errores terminan con `return`.

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

function Invoke-PolyglotInstall {
    param(
        [string]$Mode,
        [bool]$Silent
    )

    # --- 1. Pre-chequeos del entorno ---
    Write-Section "PolyglotCLI · Instalador one-liner"

    if ($env:OS -ne "Windows_NT") {
        Write-Err "Este script solo funciona en Windows."
        return
    }

    if ($PSVersionTable.PSVersion.Major -lt 5) {
        Write-Err "Necesitas PowerShell 5.1 o superior. Versión actual: $($PSVersionTable.PSVersion)"
        return
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
        return
    }

    $asset = $release.assets | Where-Object { $_.name -like "PolyglotCLI-*-x64-setup.exe" } | Select-Object -First 1
    if (-not $asset) {
        Write-Err "La ultima release ($($release.tag_name)) no incluye un instalador .exe para x64."
        return
    }

    # Defensa en profundidad: el host de descarga debe ser GitHub. Aunque el
    # JSON viene de api.github.com, validamos antes de bajar.
    $downloadUri = [Uri]$asset.browser_download_url
    if ($downloadUri.Scheme -ne "https" -or
        -not ($downloadUri.Host -ieq "github.com" -or $downloadUri.Host -ieq "objects.githubusercontent.com")) {
        Write-Err "La URL del instalador ($($downloadUri.Host)) no es un host de GitHub permitido. Abortando."
        return
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
        return
    }

    Write-Ok "Ultima version: $version ($sizeMb MB)"
    Write-Host "    $downloadUrl" -ForegroundColor DarkGray

    # --- 4. Verificar/instalar los runtimes de .NET 11 necesarios ---
    # Cada componente necesita un runtime distinto:
    #   desktop -> Microsoft.NETCore.App 11 (lo aporta el .NET Desktop Runtime)
    #   server  -> Microsoft.NETCore.App 11 + Microsoft.AspNetCore.App 11
    # El instalador de Inno Setup vuelve a verificarlo por componente.
    Write-Section "Verificando runtimes de .NET 11"

    $requiredRuntimes = @()
    if ($Mode -in @("desktop", "both")) {
        $requiredRuntimes += @{ Framework = "Microsoft.NETCore.App"; Name = ".NET 11 Desktop Runtime"; WingetId = "Microsoft.DotNet.DesktopRuntime.11" }
    }
    if ($Mode -in @("server", "both")) {
        $requiredRuntimes += @{ Framework = "Microsoft.NETCore.App"; Name = ".NET 11 Runtime"; WingetId = "Microsoft.DotNet.Runtime.11" }
        $requiredRuntimes += @{ Framework = "Microsoft.AspNetCore.App"; Name = "ASP.NET Core 11 Runtime"; WingetId = "Microsoft.DotNet.AspNetCore.11" }
    }

    function Test-DotNetRuntime([string]$Framework) {
        try {
            $runtimes = & dotnet --list-runtimes 2>$null
            return [bool]($runtimes -match ("^" + [regex]::Escape($Framework) + " 11\."))
        } catch {
            return $false
        }
    }

    foreach ($rt in $requiredRuntimes) {
        if (Test-DotNetRuntime $rt.Framework) {
            Write-Ok "$($rt.Name) OK"
            continue
        }

        Write-Warn "$($rt.Name) no detectado."
        $ans = if ($Silent) { "S" } else { Read-Host "Deseas instalarlo automaticamente via winget? (S/N)" }
        if ($ans -match "^[SsYy]?$") {
            Write-Host "Instalando $($rt.Name)..." -ForegroundColor Yellow
            try {
                winget install --id $rt.WingetId --exact --silent --accept-package-agreements --accept-source-agreements | Out-Null
            } catch {
                Write-Warn "winget fallo: $($_.Exception.Message)"
            }
        }

        if (-not (Test-DotNetRuntime $rt.Framework)) {
            Write-Warn "$($rt.Name) sigue sin detectarse. El instalador de PolyglotCLI ofrecera instalarlo;"
            Write-Warn "tambien puedes descargarlo desde https://dotnet.microsoft.com/download/dotnet/11.0"
        } else {
            Write-Ok "$($rt.Name) instalado"
        }
    }

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
        return
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
        return
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
    $installerArgs = @(
        "/VERYSILENT"
        "/SP-"
        "/CLOSEAPPLICATIONS"
        "/NORESTART"
        "/COMPONENTS=$components"
        "/TASKS=desktopicon"
        "/DIR=`"$installDir`""
    )
    if ($Silent) {
        # Sin dialogos: las preguntas del instalador (p. ej. instalar el
        # runtime de .NET que falte) toman su respuesta por defecto.
        $installerArgs += "/SUPPRESSMSGBOXES"
    }

    Write-Host "Comando: $installerPath $($installerArgs -join ' ')" -ForegroundColor DarkGray
    Write-Host ""

    $proc = Start-Process -FilePath $installerPath -ArgumentList $installerArgs -PassThru -Wait
    if ($proc.ExitCode -ne 0) {
        # Inno Setup devuelve 0/1/2 en éxito; códigos 5+ suelen ser cancelaciones
        # que ya son visibles. Si es un fallo grave lo reportamos.
        if ($proc.ExitCode -gt 2) {
            Write-Err "El instalador termino con codigo de salida $($proc.ExitCode)."
            return
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
}

Invoke-PolyglotInstall -Mode $Mode -Silent ([bool]$Silent)
