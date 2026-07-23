# scripts/bump_version.ps1
# Incrementa la version en el .csproj, actualiza el CHANGELOG.md, crea el commit/tag y hace push a GitHub.

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

# Asegurar que estamos en el directorio raiz del proyecto
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) {
    $scriptDir = $PSScriptRoot
}
if (-not $scriptDir) {
    $scriptDir = "."
}
Set-Location $scriptDir
Set-Location ".."

$csprojPath = "PolyglotCLI.web/PolyglotCLI.web.csproj"
$changelogPath = "docs/CHANGELOG.md"
$unreleasedPath = "docs/UNRELEASE.md"
$installerScript = Join-Path $scriptDir "build_installer.ps1"

# Helper: prefiere PowerShell 7 (pwsh) y cae a Windows PowerShell 5.1 si no esta.
function Get-PowerShellExe {
    $pwshCmd = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwshCmd) { return "pwsh" }
    return "powershell"
}

# Funcion para verificar cambios locales sin confirmar
function Test-GitPendingChanges {
    $status = git status --porcelain
    if ($status) {
        Write-Host "[ADVERTENCIA] Hay cambios locales sin confirmar en el repositorio:" -ForegroundColor Yellow
        Write-Host $status
        $choice = Read-Host "Desea continuar de todos modos? (y/n)"
        if ($choice -ne 'y' -and $choice -ne 'Y') {
            Write-Host "Operacion cancelada."
            return $false
        }
    }
    return $true
}

# Funcion para verificar permisos de push en origin
function Test-GitPushPermissions {
    $branch = git branch --show-current
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = "main"
    }
    
    Write-Host "Verificando autenticacion de Git y permisos de push para origin ($branch)..." -ForegroundColor Yellow
    $env:GIT_TERMINAL_PROMPT = "0"
    $oldEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    git push --dry-run origin $branch
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $oldEAP
    Remove-Item env:GIT_TERMINAL_PROMPT -ErrorAction SilentlyContinue

    if ($exitCode -ne 0) {
        Write-Error "La autenticacion de Git fallo o no tiene permisos de push a origin."
        Write-Host "Por favor, asegurese de estar autenticado en GitHub (ej. usando 'gh auth login')." -ForegroundColor Yellow
        return $false
    }
    Write-Host "Autenticacion de Git validada con exito." -ForegroundColor Green
    return $true
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    Pipeline de Release para PolyglotCLI  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Validaciones previas de Git
if (-not (Test-GitPendingChanges)) { exit 0 }
if (-not (Test-GitPushPermissions)) { exit 1 }

# 2. Cargar version actual del .csproj
if (-not (Test-Path $csprojPath)) {
    Write-Error "No se encontro $csprojPath"
    exit 1
}

$csprojContent = Get-Content -Raw -Path $csprojPath
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $currentVersion = $Matches[1]
    Write-Host "Version actual en csproj: $currentVersion" -ForegroundColor Cyan

    # Sugerir version incremental (patch)
    $parts = $currentVersion -split '\.'
    if ($parts.Count -eq 3) {
        $nextPatch = "$($parts[0]).$($parts[1]).$([int]$parts[2] + 1)"
    } else {
        $nextPatch = $currentVersion
    }

    $newVersion = Read-Host "Introduzca la nueva version [$nextPatch]"
    if ([string]::IsNullOrWhiteSpace($newVersion)) {
        $newVersion = $nextPatch
    }

    if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Formato de version no valido. Debe ser del tipo X.Y.Z (ej. 1.0.1)"
        exit 1
    }

    # 3. Leer y procesar docs/UNRELEASE.md y docs/CHANGELOG.md
    if (-not (Test-Path $unreleasedPath)) {
        Write-Error "No se encontro el archivo $unreleasedPath"
        exit 1
    }
    if (-not (Test-Path $changelogPath)) {
        # Crear archivo vacio si no existe
        New-Item -ItemType File -Path $changelogPath -Force | Out-Null
    }

    Write-Host "Procesando notas de la version en docs/UNRELEASE.md..." -ForegroundColor Yellow
    $unreleasedLines = Get-Content -Path $unreleasedPath -Encoding UTF8
    $unreleasedIndex = -1
    for ($i = 0; $i -lt $unreleasedLines.Count; $i++) {
        if ($unreleasedLines[$i].Trim() -eq "## [Unreleased]") {
            $unreleasedIndex = $i
            break
        }
    }

    if ($unreleasedIndex -eq -1) {
        Write-Error "No se encontro la seccion '## [Unreleased]' en $unreleasedPath"
        exit 1
    }

    # Extraer contenido debajo de ## [Unreleased]
    $sectionContent = @()
    if ($unreleasedIndex + 1 -lt $unreleasedLines.Count) {
        $sectionContent = $unreleasedLines[($unreleasedIndex + 1)..($unreleasedLines.Count - 1)]
    }

    # Limpiar lineas vacias iniciales y finales
    $first = 0
    $last = $sectionContent.Count - 1
    while ($first -le $last -and [string]::IsNullOrWhiteSpace($sectionContent[$first])) { $first++ }
    while ($last -ge $first -and [string]::IsNullOrWhiteSpace($sectionContent[$last])) { $last-- }

    $trimmedContent = @()
    if ($first -le $last) {
        $trimmedContent = $sectionContent[$first..$last]
    }

    if ($trimmedContent.Count -eq 0) {
        Write-Host "[ADVERTENCIA] No hay cambios detallados bajo '## [Unreleased]' en $unreleasedPath." -ForegroundColor Yellow
        $continueEmpty = Read-Host "Desea continuar con el release vacio? (y/n)"
        if ($continueEmpty -ne 'y' -and $continueEmpty -ne 'Y') {
            Write-Host "Operacion abortada."
            exit 0
        }
    }

    # Crear bloque de nueva version con la fecha actual
    $today = Get-Date -Format "yyyy-MM-dd"
    $newVersionBlock = @(
        "## [v$newVersion] - $today",
        ""
    ) + $trimmedContent

    # Leer CHANGELOG.md actual
    $changelogLines = Get-Content -Path $changelogPath -Encoding UTF8
    $newChangelogLines = @()
    $inserted = $false
    
    $hasHeader = $false
    foreach ($line in $changelogLines) {
        if ($line -match '^# Changelog') {
            $hasHeader = $true
            break
        }
    }

    # Escribir encabezado por defecto si no existe
    if (-not $hasHeader) {
        $newChangelogLines += "# Changelog"
        $newChangelogLines += ""
        $newChangelogLines += "All notable changes to this project will be documented in this file."
        $newChangelogLines += ""
        $newChangelogLines += "The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),"
        $newChangelogLines += "and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)."
        $newChangelogLines += ""
        $newChangelogLines += "---"
        $newChangelogLines += ""
    }

    # Insertar el nuevo bloque de version antes del primer bloque de version existente (## [v...)
    foreach ($line in $changelogLines) {
        if (-not $inserted -and $line -match '^## \[v\d+\.') {
            $newChangelogLines += $newVersionBlock
            $newChangelogLines += ""
            $newChangelogLines += "---"
            $newChangelogLines += ""
            $inserted = $true
        }
        $newChangelogLines += $line
    }

    # Si no habia versiones previas, se aÃ±ade al final
    if (-not $inserted) {
        if ($newChangelogLines.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($newChangelogLines[-1])) {
            $newChangelogLines += ""
        }
        $newChangelogLines += $newVersionBlock
    }

    # 4. Modificar archivos de proyecto localmente (Csproj)
    Write-Host "Modificando la version del proyecto en $csprojPath..." -ForegroundColor Yellow
    $newCsprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $csprojPath -Value $newCsprojContent -Encoding UTF8

    # Escribir docs/CHANGELOG.md actualizado
    Write-Host "Actualizando $changelogPath..." -ForegroundColor Yellow
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines((Resolve-Path $changelogPath).Path, $newChangelogLines, $utf8NoBom)

    # Restablecer docs/UNRELEASE.md a la plantilla vacia
    Write-Host "Restableciendo $unreleasedPath..." -ForegroundColor Yellow
    $template = @(
        "# Changelog",
        "",
        "All notable changes to this project will be documented in this file.",
        "",
        "The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),",
        "and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).",
        "",
        "## [Unreleased]",
        "",
        "### Added",
        "",
        "### Improved",
        "",
        "### Changed",
        "",
        "### Deprecated",
        "",
        "### Removed",
        "",
        "### Fixed"
    )
    [System.IO.File]::WriteAllLines((Resolve-Path $unreleasedPath).Path, $template, $utf8NoBom)

    # 5. Compilacion de prueba local + generacion del instalador Inno Setup
    Write-Host "Ejecutando dotnet publish y compilacion del instalador (Inno Setup)..." -ForegroundColor Yellow
    try {
        # Publicar web app + MAUI app + compilar el instalador reutilizando
        # scripts/build_installer.ps1. Requiere Inno Setup 7 instalado.
        if (-not (Test-Path $installerScript)) {
            Write-Error "No se encontro el script $installerScript"
            exit 1
        }

        & (Get-PowerShellExe) -NoLogo -NoProfile -File $installerScript -Version $newVersion
        if ($LASTEXITCODE -ne 0) {
            throw "build_installer.ps1 devolvio codigo de salida $LASTEXITCODE"
        }
        Write-Host "Compilacion de prueba exitosa." -ForegroundColor Green
    } catch {
        Write-Error "La compilacion fallo. Revirtiendo cambios locales..."
        Set-Content -Path $csprojPath -Value $csprojContent -Encoding UTF8
        if (Test-Path $wxsPath) {
            Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8
        }
        git checkout -- $changelogPath $unreleasedPath
        Write-Host "Cambios revertidos correctamente." -ForegroundColor Yellow
        exit 1
    }

    # 6. Confirmacion de Git
    $branch = git branch --show-current
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = "main"
    }

    # Capturar el estado pendiente antes de stagear (modificaciones + archivos sin seguimiento)
    # para mostrar al usuario exactamente qué entrara en el commit.
    $pendingStatus = git status --porcelain

    Write-Host ""
    Write-Host "Resumen de acciones a ejecutar en Git:" -ForegroundColor Yellow
    Write-Host "  - Stage de TODOS los archivos pendientes (git add -A, respetando .gitignore):" -ForegroundColor Yellow
    if ([string]::IsNullOrWhiteSpace($pendingStatus)) {
        Write-Host "      (no hay cambios locales pendientes; solo entraran los archivos de version)" -ForegroundColor DarkGray
    } else {
        foreach ($line in $pendingStatus -split "`n") {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            Write-Host "      $line"
        }
    }
    Write-Host "  - Crear etiqueta Git v$newVersion"
    Write-Host "  - Push de commits y etiquetas a origin ($branch)"
    Write-Host ""

    $confirm = Read-Host "Esta seguro de querer confirmar, etiquetar y subir a GitHub? (y/n)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Acciones de Git canceladas. Los archivos locales han sido actualizados pero no commiteados ni subidos." -ForegroundColor Yellow
        exit 0
    }

    # Git add: Stagear todos los cambios pendientes (modificaciones, archivos nuevos
    # y borrados) respetando el .gitignore. Es equivalente a `git add --all`.
    Write-Host "Agregando todos los archivos pendientes a Git..." -ForegroundColor Yellow
    git add -A

    # Si git add -A no encontro nada nuevo (solo se modificaron los archivos de
    # version que de todos modos el commit capturara), nos aseguramos de que esos
    # esten explícitamente en el stage.
    git add $csprojPath
    git add $changelogPath
    git add $unreleasedPath

    git commit -m "Bump version to v$newVersion"
    git tag -a "v$newVersion" -m "Release v$newVersion"

    Write-Host "Subiendo commits y etiqueta a GitHub..." -ForegroundColor Yellow
    try {
        git push origin $branch
        git push origin "v$newVersion"
        Write-Host "Version v$newVersion publicada y subida con exito en GitHub!" -ForegroundColor Green
    } catch {
        Write-Error "Error al subir cambios a GitHub. Verifique su conexion y permisos."
        Write-Host "Nota: El commit y la etiqueta se crearon localmente. Puede subirlos manualmente usando:" -ForegroundColor Yellow
        Write-Host "  git push origin $branch" -ForegroundColor Yellow
        Write-Host "  git push origin v$newVersion" -ForegroundColor Yellow
    }
} else {
    Write-Error "No se pudo encontrar la etiqueta <Version> en $csprojPath"
    exit 1
}

