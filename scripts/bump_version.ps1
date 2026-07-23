# bump_version.ps1
# Incrementa la versión en el .csproj, actualiza el CHANGELOG.md, crea el commit/tag y hace push a GitHub.

$ErrorActionPreference = "Stop"

# Asegurar que estamos en el directorio correcto
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir) {
    Set-Location $scriptDir
    Set-Location ..
}

$csprojPath = "PolyglotCLI.web/PolyglotCLI.web.csproj"
$changelogPath = "CHANGELOG.md"

if (-not (Test-Path $csprojPath)) {
    Write-Error "No se encontró $csprojPath. Asegúrese de ejecutar este script desde la carpeta scripts."
    exit 1
}

# Obtener rama actual
$branch = git branch --show-current
if ([string]::IsNullOrWhiteSpace($branch)) {
    $branch = "main"
}

# Comprobación preliminar de permisos de push
Write-Host "Verificando autenticación y permisos de push en origin ($branch)..." -ForegroundColor Yellow
$env:GIT_TERMINAL_PROMPT = "0"
$oldEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
git push --dry-run origin $branch
$exitCode = $LASTEXITCODE
$ErrorActionPreference = $oldEAP
Remove-Item env:GIT_TERMINAL_PROMPT -ErrorAction SilentlyContinue

if ($exitCode -ne 0) {
    Write-Error "La autenticación de Git falló o no tiene permisos de push a origin."
    Write-Host "Por favor, asegúrese de estar autenticado en GitHub (ej. usando 'gh auth login' o credenciales activas)." -ForegroundColor Yellow
    exit 1
}
Write-Host "Permisos de Git validados con éxito." -ForegroundColor Green

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "     PolyglotCLI Version Bump & Release   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Comprobar si hay cambios locales sin confirmar
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "[ADVERTENCIA] Tiene cambios sin confirmar en el repositorio:" -ForegroundColor Yellow
    Write-Host $gitStatus
    $choice = Read-Host "Desea proceder de todos modos? (y/n)"
    if ($choice -ne 'y' -and $choice -ne 'Y') {
        Write-Host "Operación cancelada."
        exit 0
    }
}

# 2. Obtener versión actual del csproj
$csprojContent = Get-Content -Raw -Path $csprojPath
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $currentVersion = $Matches[1]
    Write-Host "Versión actual en csproj: $currentVersion" -ForegroundColor Cyan

    # Sugerir la siguiente versión de parche
    $parts = $currentVersion -split '\.'
    if ($parts.Count -eq 3) {
        $nextPatch = "$($parts[0]).$($parts[1]).$([int]$parts[2] + 1)"
    } else {
        $nextPatch = $currentVersion
    }

    $newVersion = Read-Host "Introduzca la nueva versión [$nextPatch]"
    if ([string]::IsNullOrWhiteSpace($newVersion)) {
        $newVersion = $nextPatch
    }

    if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Formato de versión no válido. Debe ser del tipo X.Y.Z"
        exit 1
    }

    # 3. Modificar csproj
    Write-Host "Actualizando $csprojPath a la versión $newVersion..." -ForegroundColor Yellow
    $newCsprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $csprojPath -Value $newCsprojContent -Encoding UTF8

    # 4. Estampar CHANGELOG.md
    if (Test-Path $changelogPath) {
        Write-Host "Actualizando CHANGELOG.md..." -ForegroundColor Yellow
        $today = (Get-Date -Format "yyyy-MM-dd")
        $changelogLines = Get-Content -Path $changelogPath -Encoding UTF8
        
        $unreleasedIndex = -1
        $nextHeaderIndex = -1
        
        for ($i = 0; $i -lt $changelogLines.Count; $i++) {
            $line = $changelogLines[$i].Trim()
            if ($line -eq "## [Unreleased]") {
                $unreleasedIndex = $i
            } elseif ($unreleasedIndex -ne -1 -and $line -match '^## \[') {
                $nextHeaderIndex = $i
                break
            }
        }

        if ($unreleasedIndex -ne -1) {
            # Extraer contenido de la sección Unreleased
            $endIdx = if ($nextHeaderIndex -ne -1) { $nextHeaderIndex - 1 } else { $changelogLines.Count - 1 }
            $unreleasedSectionLines = @()
            if ($endIdx -ge ($unreleasedIndex + 1)) {
                $unreleasedSectionLines = $changelogLines[($unreleasedIndex + 1)..$endIdx]
            }

            # Recortar líneas vacías
            $first = 0
            $last = $unreleasedSectionLines.Count - 1
            while ($first -le $last -and [string]::IsNullOrWhiteSpace($unreleasedSectionLines[$first])) { $first++ }
            while ($last -ge $first -and [string]::IsNullOrWhiteSpace($unreleasedSectionLines[$last])) { $last-- }
            
            $sectionContent = @()
            if ($first -le $last) {
                $sectionContent = $unreleasedSectionLines[$first..$last]
            }

            # Crear el bloque nuevo de la versión
            $newVersionBlock = @(
                "## [v$newVersion] - $today",
                ""
            ) + $sectionContent

            # Crear la plantilla limpia para Unreleased
            $unreleasedTemplate = @(
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
                "### Fixed",
                ""
            )

            # Reconstruir CHANGELOG.md
            $beforeUnreleased = $changelogLines[0..($unreleasedIndex - 1)]
            $afterUnreleased = if ($nextHeaderIndex -ne -1) { $changelogLines[$nextHeaderIndex..($changelogLines.Count - 1)] } else { @() }

            $newChangelogLines = $beforeUnreleased + $unreleasedTemplate + $newVersionBlock + "" + $afterUnreleased

            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllLines((Resolve-Path $changelogPath).Path, $newChangelogLines, $utf8NoBom)
            Write-Host "CHANGELOG.md actualizado con éxito." -ForegroundColor Green
        } else {
            Write-Host "[ADVERTENCIA] No se encontró la sección '## [Unreleased]' en CHANGELOG.md." -ForegroundColor Yellow
        }
    } else {
        Write-Host "[ADVERTENCIA] No se encontró CHANGELOG.md en la raíz." -ForegroundColor Yellow
    }

    # 5. Ejecutar compilación de prueba local
    Write-Host "Ejecutando dotnet build de prueba..." -ForegroundColor Yellow
    try {
        dotnet build PolyglotCLI.web/PolyglotCLI.web.csproj -c Release
    } catch {
        Write-Error "La compilación de prueba falló. Revise los errores e intente de nuevo."
        exit 1
    }

    # 6. Confirmación de Git
    Write-Host "Resumen de acciones a ejecutar:" -ForegroundColor Yellow
    Write-Host "  - Hacer commit de $csprojPath y CHANGELOG.md"
    Write-Host "  - Crear etiqueta Git v$newVersion"
    Write-Host "  - Pushing commits y etiquetas a origin ($branch)"
    Write-Host ""

    $confirm = Read-Host "Está seguro de querer confirmar, etiquetar y subir los cambios? (y/n)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Los archivos locales fueron actualizados pero no se ha realizado ninguna acción en Git." -ForegroundColor Yellow
        exit 0
    }

    # Git commit y push
    git add $csprojPath
    if (Test-Path $changelogPath) {
        git add $changelogPath
    }

    git commit -m "Bump version to v$newVersion"
    git tag -a "v$newVersion" -m "Release v$newVersion"

    Write-Host "Enviando commits y etiqueta a origin..." -ForegroundColor Yellow
    try {
        git push origin $branch
        git push origin "v$newVersion"
        Write-Host "Versión v$newVersion publicada y subida con éxito en GitHub!" -ForegroundColor Green
    } catch {
        Write-Error "Error al subir cambios a GitHub. Verifique su conexión y permisos."
        Write-Host "Nota: El commit y la etiqueta se crearon localmente. Puede subirlos manualmente usando:" -ForegroundColor Yellow
        Write-Host "  git push origin $branch" -ForegroundColor Yellow
        Write-Host "  git push origin v$newVersion" -ForegroundColor Yellow
    }
} else {
    Write-Error "No se pudo encontrar la etiqueta <Version> en $csprojPath"
    exit 1
}
