# scripts/bump_version.ps1
# Incrementa la versiÃ³n en el .csproj y Package.wxs, actualiza el CHANGELOG.md, crea el commit/tag y hace push a GitHub.

$ErrorActionPreference = "Stop"

# Asegurar que estamos en el directorio raÃ­z del proyecto
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir) {
    Set-Location $scriptDir
    Set-Location ..
}

$csprojPath = "PolyglotCLI.web/PolyglotCLI.web.csproj"
$wxsPath = "PolyglotCLI.Wix/Package.wxs"
$changelogPath = "docs/CHANGELOG.md"
$unreleasedPath = "docs/UNRELEASE.md"

# FunciÃ³n para verificar cambios locales sin confirmar
function Test-GitPendingChanges {
    $status = git status --porcelain
    if ($status) {
        Write-Host "[ADVERTENCIA] Hay cambios locales sin confirmar en el repositorio:" -ForegroundColor Yellow
        Write-Host $status
        $choice = Read-Host "Desea continuar de todos modos? (y/n)"
        if ($choice -ne 'y' -and $choice -ne 'Y') {
            Write-Host "OperaciÃ³n cancelada."
            return $false
        }
    }
    return $true
}

# FunciÃ³n para verificar permisos de push en origin
function Test-GitPushPermissions {
    $branch = git branch --show-current
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = "main"
    }
    
    Write-Host "Verificando autenticaciÃ³n de Git y permisos de push para origin ($branch)..." -ForegroundColor Yellow
    $env:GIT_TERMINAL_PROMPT = "0"
    $oldEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    git push --dry-run origin $branch
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $oldEAP
    Remove-Item env:GIT_TERMINAL_PROMPT -ErrorAction SilentlyContinue

    if ($exitCode -ne 0) {
        Write-Error "La autenticaciÃ³n de Git fallÃ³ o no tiene permisos de push a origin."
        Write-Host "Por favor, asegÃºrese de estar autenticado en GitHub (ej. usando 'gh auth login')." -ForegroundColor Yellow
        return $false
    }
    Write-Host "AutenticaciÃ³n de Git validada con Ã©xito." -ForegroundColor Green
    return $true
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    Pipeline de Release para PolyglotCLI  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Validaciones previas de Git
if (-not (Test-GitPendingChanges)) { exit 0 }
if (-not (Test-GitPushPermissions)) { exit 1 }

# 2. Cargar versiÃ³n actual del .csproj
if (-not (Test-Path $csprojPath)) {
    Write-Error "No se encontrÃ³ $csprojPath"
    exit 1
}

$csprojContent = Get-Content -Raw -Path $csprojPath
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $currentVersion = $Matches[1]
    Write-Host "VersiÃ³n actual en csproj: $currentVersion" -ForegroundColor Cyan

    # Sugerir versiÃ³n incremental (patch)
    $parts = $currentVersion -split '\.'
    if ($parts.Count -eq 3) {
        $nextPatch = "$($parts[0]).$($parts[1]).$([int]$parts[2] + 1)"
    } else {
        $nextPatch = $currentVersion
    }

    $newVersion = Read-Host "Introduzca la nueva versiÃ³n [$nextPatch]"
    if ([string]::IsNullOrWhiteSpace($newVersion)) {
        $newVersion = $nextPatch
    }

    if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Formato de versiÃ³n no vÃ¡lido. Debe ser del tipo X.Y.Z (ej. 1.0.1)"
        exit 1
    }

    # 3. Leer y procesar docs/UNRELEASE.md y docs/CHANGELOG.md
    if (-not (Test-Path $unreleasedPath)) {
        Write-Error "No se encontrÃ³ el archivo $unreleasedPath"
        exit 1
    }
    if (-not (Test-Path $changelogPath)) {
        # Crear archivo vacÃ­o si no existe
        New-Item -ItemType File -Path $changelogPath -Force | Out-Null
    }

    Write-Host "Procesando notas de la versiÃ³n en docs/UNRELEASE.md..." -ForegroundColor Yellow
    $unreleasedLines = Get-Content -Path $unreleasedPath -Encoding UTF8
    $unreleasedIndex = -1
    for ($i = 0; $i -lt $unreleasedLines.Count; $i++) {
        if ($unreleasedLines[$i].Trim() -eq "## [Unreleased]") {
            $unreleasedIndex = $i
            break
        }
    }

    if ($unreleasedIndex -eq -1) {
        Write-Error "No se encontrÃ³ la secciÃ³n '## [Unreleased]' en $unreleasedPath"
        exit 1
    }

    # Extraer contenido debajo de ## [Unreleased]
    $sectionContent = @()
    if ($unreleasedIndex + 1 -lt $unreleasedLines.Count) {
        $sectionContent = $unreleasedLines[($unreleasedIndex + 1)..($unreleasedLines.Count - 1)]
    }

    # Limpiar lÃ­neas vacÃ­as iniciales y finales
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
        $continueEmpty = Read-Host "Desea continuar con el release vacÃ­o? (y/n)"
        if ($continueEmpty -ne 'y' -and $continueEmpty -ne 'Y') {
            Write-Host "OperaciÃ³n abortada."
            exit 0
        }
    }

    # Crear bloque de nueva versiÃ³n con la fecha actual
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

    # Insertar el nuevo bloque de versiÃ³n antes del primer bloque de versiÃ³n existente (## [v...)
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

    # Si no habÃ­a versiones previas, se aÃ±ade al final
    if (-not $inserted) {
        if ($newChangelogLines.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($newChangelogLines[-1])) {
            $newChangelogLines += ""
        }
        $newChangelogLines += $newVersionBlock
    }

    # 4. Modificar archivos de proyecto localmente (Csproj y Wix)
    Write-Host "Modificando la versiÃ³n del proyecto en $csprojPath..." -ForegroundColor Yellow
    $newCsprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $csprojPath -Value $newCsprojContent -Encoding UTF8

    if (Test-Path $wxsPath) {
        Write-Host "Modificando la versiÃ³n de Wix en $wxsPath..." -ForegroundColor Yellow
        $wxsContent = Get-Content -Raw -Path $wxsPath
        $newWxsContent = $wxsContent -replace 'Version="[^"]+"', "Version=`"$newVersion`""
        Set-Content -Path $wxsPath -Value $newWxsContent -Encoding UTF8
    }

    # Escribir docs/CHANGELOG.md actualizado
    Write-Host "Actualizando $changelogPath..." -ForegroundColor Yellow
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines((Resolve-Path $changelogPath).Path, $newChangelogLines, $utf8NoBom)

    # Restablecer docs/UNRELEASE.md a la plantilla vacÃ­a
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

    # 5. CompilaciÃ³n de prueba local
    Write-Host "Ejecutando dotnet build de validaciÃ³n..." -ForegroundColor Yellow
    try {
        # Primero compilar web app
        dotnet build $csprojPath -c Release
        # Luego compilar el instalador de WiX (si existe)
        if (Test-Path "PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj") {
            dotnet build PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj -c Release
        }
        Write-Host "CompilaciÃ³n de prueba exitosa." -ForegroundColor Green
    } catch {
        Write-Error "La compilaciÃ³n fallÃ³. Revirtiendo cambios locales..."
        # Revertir csproj
        Set-Content -Path $csprojPath -Value $csprojContent -Encoding UTF8
        # Revertir wxs si existÃ­a
        if (Test-Path $wxsPath) {
            Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8
        }
        # Revertir changelog y unreleased de git
        git checkout -- $changelogPath $unreleasedPath
        Write-Host "Cambios revertidos correctamente." -ForegroundColor Yellow
        exit 1
    }

    # 6. ConfirmaciÃ³n de Git
    $branch = git branch --show-current
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = "main"
    }

    Write-Host ""
    Write-Host "Resumen de acciones a ejecutar en Git:" -ForegroundColor Yellow
    Write-Host "  - Stage y commit de los archivos modificados:"
    Write-Host "      $csprojPath"
    if (Test-Path $wxsPath) { Write-Host "      $wxsPath" }
    Write-Host "      $changelogPath"
    Write-Host "      $unreleasedPath"
    Write-Host "  - Crear etiqueta Git v$newVersion"
    Write-Host "  - Push de commits y etiquetas a origin ($branch)"
    Write-Host ""

    $confirm = Read-Host "EstÃ¡ seguro de querer confirmar, etiquetar y subir a GitHub? (y/n)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Acciones de Git canceladas. Los archivos locales han sido actualizados pero no commiteados ni subidos." -ForegroundColor Yellow
        exit 0
    }

    # Git commit, tag y push
    Write-Host "Agregando archivos a Git..." -ForegroundColor Yellow
    git add $csprojPath
    if (Test-Path $wxsPath) { git add $wxsPath }
    git add $changelogPath
    git add $unreleasedPath

    git commit -m "Bump version to v$newVersion"
    git tag -a "v$newVersion" -m "Release v$newVersion"

    Write-Host "Subiendo commits y etiqueta a GitHub..." -ForegroundColor Yellow
    try {
        git push origin $branch
        git push origin "v$newVersion"
        Write-Host "VersiÃ³n v$newVersion publicada y subida con Ã©xito en GitHub!" -ForegroundColor Green
    } catch {
        Write-Error "Error al subir cambios a GitHub. Verifique su conexiÃ³n y permisos."
        Write-Host "Nota: El commit y la etiqueta se crearon localmente. Puede subirlos manualmente usando:" -ForegroundColor Yellow
        Write-Host "  git push origin $branch" -ForegroundColor Yellow
        Write-Host "  git push origin v$newVersion" -ForegroundColor Yellow
    }
} else {
    Write-Error "No se pudo encontrar la etiqueta <Version> en $csprojPath"
    exit 1
}


