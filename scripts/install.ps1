param (
    [switch]$DebugMode = $false,
    [switch]$FromSource = $false,
    [switch]$Uninstall = $false
)

# Permitir pasar argumentos como texto sin guión (ej. "uninstall" o "debug")
if ($args -contains "debug") { $DebugMode = $true }
if ($args -contains "fromsource" -or $args -contains "source") { $FromSource = $true }
if ($args -contains "uninstall") { $Uninstall = $true }

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "."
}

$repo = "FittyAr/PolyglotCLI"
$appName = "PolyglotCLI"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$configDir = Join-Path $env:APPDATA $appName
$exePath = Join-Path $installDir "$appName.exe"

# Función para verificar si el ASP.NET Core Runtime 10.x está instalado
function Test-DotNetRuntimeInstalled {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }
    
    $runtimes = dotnet --list-runtimes
    # Buscamos la presencia de Microsoft.AspNetCore.App versión 10.x
    $hasAspNetCore10 = $runtimes | Where-Object { $_ -match "^Microsoft\.AspNetCore\.App\s+10\." }
    
    if ($hasAspNetCore10) {
        return $true
    }
    return $false
}

# Flujo de desinstalación
if ($Uninstall) {
    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host "   Desinstalador de PolyglotCLI para Windows  " -ForegroundColor Blue
    Write-Host "==========================================" -ForegroundColor Blue

    # 1. Detener procesos en ejecución
    Write-Host "Deteniendo procesos activos de PolyglotCLI..." -ForegroundColor Yellow
    Stop-Process -Name "PolyglotCLI" -Force -ErrorAction SilentlyContinue
    Stop-Process -Name "PolyglotCLI.web" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1

    # 2. Eliminar accesos directos
    $startMenuLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PolyglotCLI.lnk"
    $desktopLnk = Join-Path $env:USERPROFILE "Desktop\PolyglotCLI.lnk"

    if (Test-Path $startMenuLnk) {
        Write-Host "Eliminando acceso directo del Menú Inicio..." -ForegroundColor Yellow
        Remove-Item -Path $startMenuLnk -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $desktopLnk) {
        Write-Host "Eliminando acceso directo del Escritorio..." -ForegroundColor Yellow
        Remove-Item -Path $desktopLnk -Force -ErrorAction SilentlyContinue
    }

    # 3. Eliminar archivos del programa
    if (Test-Path $installDir) {
        Write-Host "Eliminando directorio del programa: $installDir" -ForegroundColor Yellow
        Remove-Item -Recurse -Force $installDir -ErrorAction SilentlyContinue
    }

    # 4. Preguntar si se eliminan los datos de configuración e historial
    if (Test-Path $configDir) {
        Write-Host ""
        Write-Host "Se encontraron configuraciones, historiales de trabajo y cachés en:" -ForegroundColor Yellow
        Write-Host "  $configDir" -ForegroundColor Yellow
        $deleteConfig = Read-Host "Desea eliminar la configuración y todos los trabajos guardados? [y/N]"
        if ($deleteConfig -match "^[yY](es)?$") {
            Write-Host "Eliminando datos de usuario..." -ForegroundColor Yellow
            Remove-Item -Recurse -Force $configDir -ErrorAction SilentlyContinue
            Write-Host "Datos de usuario eliminados." -ForegroundColor Green
        } else {
            Write-Host "Configuración conservada." -ForegroundColor Green
        }
    }

    # 5. Remover de PATH
    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($userPath -split ';' -contains $installDir) {
        Write-Host "Removiendo PolyglotCLI de la variable PATH del usuario..." -ForegroundColor Yellow
        $pathList = $userPath -split ';' | Where-Object { $_ -ne $installDir }
        $newPath = $pathList -join ';'
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Host "PATH de usuario actualizado." -ForegroundColor Green
    }

    Write-Host "==========================================" -ForegroundColor Blue
    Write-Host "Desinstalación de PolyglotCLI completada con éxito!" -ForegroundColor Green
    exit 0
}

Write-Host "==========================================" -ForegroundColor Blue
Write-Host "    Instalador de PolyglotCLI para Windows    " -ForegroundColor Blue
Write-Host "==========================================" -ForegroundColor Blue

# 1. Comprobar e instalar ASP.NET Core Runtime 10 si falta
Write-Host "Verificando dependencias de .NET..." -ForegroundColor Yellow
$runtimeInstalled = Test-DotNetRuntimeInstalled

if (-not $runtimeInstalled) {
    Write-Host "ASP.NET Core Runtime 10.x no detectado. Es necesario instalarlo." -ForegroundColor Yellow
    
    # Intentar instalar con Winget
    $installedWithWinget = $false
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host "Intentando instalar Microsoft ASP.NET Core Runtime 10.0 vía WinGet..." -ForegroundColor Yellow
        try {
            # Lanzamos winget y esperamos a que termine
            Start-Process -FilePath "winget" -ArgumentList "install --id Microsoft.DotNet.AspNetCore.10 --silent --accept-package-agreements --accept-source-agreements" -Wait -NoNewWindow
            $installedWithWinget = $true
        } catch {
            Write-Host "La instalación con winget falló o fue denegada." -ForegroundColor Red
        }
    }
    
    # Fallback si Winget no está o falló
    if (-not $installedWithWinget) {
        $dotnetVersion = "10.0.10"
        $installerName = "aspnetcore-runtime-$dotnetVersion-win-x64.exe"
        $downloadUrl = "https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$dotnetVersion/$installerName"
        $tempInstallerPath = Join-Path $env:TEMP $installerName
        
        Write-Host "Descargando ASP.NET Core Runtime $dotnetVersion desde Microsoft..." -ForegroundColor Yellow
        Write-Host "URL: $downloadUrl" -ForegroundColor Cyan
        
        try {
            Invoke-WebRequest -Uri $downloadUrl -OutFile $tempInstallerPath -UseBasicParsing
            Write-Host "Ejecutando instalador de .NET de forma silenciosa (puede solicitar permisos de Administrador)..." -ForegroundColor Yellow
            
            # Ejecutar instalador y esperar
            $proc = Start-Process -FilePath $tempInstallerPath -ArgumentList "/install /quiet /norestart" -Wait -PassThru -NoNewWindow
            if ($proc.ExitCode -ne 0) {
                Write-Error "El instalador de .NET devolvió el código de salida: $($proc.ExitCode)"
            }
            Remove-Item -Path $tempInstallerPath -Force -ErrorAction SilentlyContinue
        } catch {
            Write-Error "Error al descargar o instalar ASP.NET Core Runtime: $_"
            exit 1
        }
    }
    
    # Recargar el PATH para detectar el nuevo binario dotnet
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    
    # Validar de nuevo
    if (-not (Test-DotNetRuntimeInstalled)) {
        Write-Error "No se pudo instalar o detectar ASP.NET Core Runtime 10.x. Por favor, instálelo manualmente desde: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    }
    Write-Host "ASP.NET Core Runtime 10.0 instalado correctamente." -ForegroundColor Green
} else {
    Write-Host "ASP.NET Core Runtime 10.x ya está instalado." -ForegroundColor Green
}

# 2. Compilar desde la fuente o Descargar de GitHub
$tempDir = Join-Path $env:TEMP "polyglotcli_install_$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

$publishSourceDir = ""
$shouldBuildFromSource = $DebugMode -or $FromSource

if (-not $shouldBuildFromSource) {
    Write-Host "Obteniendo información del último release desde GitHub..." -ForegroundColor Yellow
    $releasesUrl = "https://api.github.com/repos/$repo/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $releasesUrl -UseBasicParsing
        $version = $release.tag_name
    } catch {
        Write-Host "No se pudo obtener información del release en GitHub (puede deberse a que no hay releases publicados o error de red)." -ForegroundColor Yellow
        $hasLocalSource = Test-Path "PolyglotCLI.web/PolyglotCLI.web.csproj"
        $hasDotNetSdk = Get-Command dotnet -ErrorAction SilentlyContinue
        
        if ($hasLocalSource -and $hasDotNetSdk) {
            Write-Host ""
            $choice = Read-Host "Se detectó código fuente local y el SDK de .NET. Desea compilar e instalar desde las fuentes locales en su lugar? [Y/n]"
            if ($choice -notmatch "^[nN]$") {
                $shouldBuildFromSource = $true
            }
        }
        
        if (-not $shouldBuildFromSource) {
            Write-Error "No se pudo continuar con la instalación: $_"
            Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
            exit 1
        }
    }
}

if ($shouldBuildFromSource) {
    Write-Host "Compilando aplicación desde el código fuente local..." -ForegroundColor Yellow
    
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Error "El SDK de .NET es necesario para compilar pero no fue hallado en PATH."
        exit 1
    }

    $publishSourceDir = Join-Path $tempDir "publish"
    try {
        dotnet publish PolyglotCLI.web/PolyglotCLI.web.csproj -c Release -r win-x64 --self-contained false -o $publishSourceDir
    } catch {
        Write-Error "Fallo en la publicación de dotnet: $_"
        Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
        exit 1
    }
} else {
    Write-Host "Última versión detectada: $version" -ForegroundColor Green
    
    $zipName = "PolyglotCLI-$version-win-x64.zip"
    $downloadUrl = "https://github.com/$repo/releases/download/$version/$zipName"
    $zipPath = Join-Path $tempDir $zipName

    Write-Host "Descargando el paquete $zipName..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
    } catch {
        Write-Error "Fallo al descargar el archivo empaquetado: $_"
        Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
        exit 1
    }

    Write-Host "Extrayendo archivos..." -ForegroundColor Yellow
    try {
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force
    } catch {
        Write-Error "Fallo al extraer el archivo ZIP: $_"
        Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
        exit 1
    }
    
    # Determinar carpeta de origen de los archivos extraídos
    $publishSourceDir = $tempDir
    # Si la extracción creó una carpeta interna, ajustamos la ruta
    $subDir = Join-Path $tempDir "publish_out"
    if (Test-Path $subDir) {
        $publishSourceDir = $subDir
    }
}

# 3. Detener procesos antiguos para poder sobreescribir los archivos
Stop-Process -Name "PolyglotCLI" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "PolyglotCLI.web" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 4. Copiar archivos al directorio final
Write-Host "Instalando archivos del programa en $installDir..." -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
}

# Copiar contenido
Copy-Item -Path "$publishSourceDir\*" -Destination $installDir -Force -Recurse

# Copiar prompts si no se copiaron
if (-not (Test-Path (Join-Path $installDir "prompts"))) {
    $srcPrompts = Join-Path $scriptDir "prompts"
    if (Test-Path $srcPrompts) {
        Copy-Item -Path $srcPrompts -Destination $installDir -Force -Recurse
    }
}

# Limpiar directorio temporal
Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue

# 5. Configurar archivo de ajustes inicial sin datos secretos
$localConfigTemplatePath = Join-Path $installDir "config.json"
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Force -Path $configDir | Out-Null
}

$targetConfigPath = Join-Path $configDir "config.json"
if (-not (Test-Path $targetConfigPath)) {
    Write-Host "Inicializando archivo de configuración limpio en $targetConfigPath..." -ForegroundColor Yellow
    if (Test-Path $localConfigTemplatePath) {
        # Copiar plantilla base
        Copy-Item -Path $localConfigTemplatePath -Destination $targetConfigPath -Force
        
        # Sanitizar y asegurar que no hay secretos
        try {
            $configObj = Get-Content -Raw -Path $targetConfigPath | ConvertFrom-Json
            
            # Limpiar ApiKeys
            $configObj.ApiKey = ""
            if ($configObj.ProviderApiKeys) {
                $configObj.ProviderApiKeys = New-Object PSObject
            }
            # Establecer URL local neutra por defecto
            $configObj.ApiUrl = "http://localhost:1234/v1"
            
            $sanitizedJson = ConvertTo-Json $configObj -Depth 10
            Set-Content -Path $targetConfigPath -Value $sanitizedJson -Encoding UTF8
        } catch {
            Write-Host "[ADVERTENCIA] Fallo al sanitizar la plantilla de configuración, se mantuvieron valores por defecto." -ForegroundColor Yellow
        }
    } else {
        # Crear config básico por defecto si no existe la plantilla
        $defaultConfig = @{
            ApiUrl = "http://localhost:1234/v1"
            DefaultModel = "qwen/qwen3.5-9b"
            DefaultVisionModel = "qwen/qwen2.5-vl-7b"
            TargetLanguage = "Spanish"
            OutputDirectory = "output"
            Debug = $false
            LogLevelConsole = "Information"
            LogLevelFile = "Debug"
        }
        $defaultConfig | ConvertTo-Json -Depth 10 | Set-Content -Path $targetConfigPath -Encoding UTF8
    }
} else {
    Write-Host "Configuración existente en $targetConfigPath detectada. Conservando ajustes del usuario." -ForegroundColor Green
}

# 6. Crear Accesos Directos
Write-Host "Creando accesos directos..." -ForegroundColor Yellow
$startMenuLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PolyglotCLI.lnk"
$desktopLnk = Join-Path $env:USERPROFILE "Desktop\PolyglotCLI.lnk"

# Script para generar acceso directo via WshShell
$WshShell = New-Object -ComObject WScript.Shell

# Acceso directo en Menú Inicio
try {
    $Shortcut = $WshShell.CreateShortcut($startMenuLnk)
    $Shortcut.TargetPath = $exePath
    $Shortcut.WorkingDirectory = $installDir
    $Shortcut.Description = "Herramienta de Traducción Documental Web PolyglotCLI"
    $Shortcut.Save()
    Write-Host "Acceso directo en Menú Inicio creado con éxito." -ForegroundColor Green
} catch {
    Write-Host "[ADVERTENCIA] No se pudo crear el acceso directo en el Menú Inicio." -ForegroundColor Yellow
}

# Acceso directo en Escritorio (Preguntar)
$desktopConfirm = Read-Host "Desea crear un acceso directo en el Escritorio? [y/N]"
if ($desktopConfirm -match "^[yY](es)?$") {
    try {
        $Shortcut = $WshShell.CreateShortcut($desktopLnk)
        $Shortcut.TargetPath = $exePath
        $Shortcut.WorkingDirectory = $installDir
        $Shortcut.Description = "Herramienta de Traducción Documental Web PolyglotCLI"
        $Shortcut.Save()
        Write-Host "Acceso directo en Escritorio creado con éxito." -ForegroundColor Green
    } catch {
        Write-Host "[ADVERTENCIA] No se pudo crear el acceso directo en el Escritorio." -ForegroundColor Yellow
    }
}

# 7. Registrar en el PATH del Usuario
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -split ';' -notcontains $installDir) {
    Write-Host "Agregando PolyglotCLI a la variable PATH del usuario..." -ForegroundColor Yellow
    [Environment]::SetEnvironmentVariable("PATH", "$userPath;$installDir", "User")
    # Actualizar PATH del proceso actual
    $env:PATH = "$env:PATH;$installDir"
    Write-Host "PATH de usuario actualizado correctamente." -ForegroundColor Green
} else {
    Write-Host "El directorio de instalación ya está en la variable PATH del usuario." -ForegroundColor Green
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Blue
Write-Host "       Instalación Completada con Éxito!  " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Blue
Write-Host "Ruta de Instalación: $installDir" -ForegroundColor Cyan
Write-Host "Ruta de Configuración: $configDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Para iniciar la aplicación:" -ForegroundColor Green
Write-Host "  1. Ejecute 'PolyglotCLI' desde PowerShell/CMD." -ForegroundColor Green
Write-Host "  2. O use el acceso directo del Menú de Inicio o Escritorio." -ForegroundColor Green
Write-Host ""
