# scripts/sync-docs.ps1
# Copia SOLO el LICENSE de la raíz del repo a los wwwroot/ de los
# proyectos web y MAUI. Es el único archivo que se sirve localmente
# desde el AboutConfigTab: el resto (README.md, diagramas) está
# vinculado directamente a GitHub para que los cambios se reflejen
# sin necesidad de un build.
#
# El target SyncDocs en PolyglotCLI.web.csproj y PolyglotCLI.Maui.csproj
# corre este script automáticamente antes de cada build, así el
# LICENSE siempre queda fresco si lo modificás.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Resolver rutas absolutas desde la raíz del repo
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = (Resolve-Path (Join-Path $scriptDir "..")).Path

$targets = @(
    (Join-Path $repoRoot "PolyglotCLI.web\wwwroot")
    (Join-Path $repoRoot "PolyglotCLI.Maui\wwwroot")
)

# Whitelist: ruta en la raíz del repo -> ruta dentro de wwwroot
# Si más adelante necesitás exponer otro archivo estático, agregá
# una entrada acá. README y diagramas NO van: el AboutConfigTab los
# apunta a GitHub para evitar rebuilds cuando cambian.
$files = @(
    @{ Src = "LICENSE";  Dst = "LICENSE" }
)

foreach ($f in $files) {
    $srcAbs = Join-Path $repoRoot $f.Src
    if (-not (Test-Path $srcAbs)) {
        Write-Warn "No existe $srcAbs, se omite."
        continue
    }
    foreach ($target in $targets) {
        $dstAbs = Join-Path $target $f.Dst
        Copy-Item -Path $srcAbs -Destination $dstAbs -Force
    }
    Write-Host "OK  $($f.Src) -> */$($f.Dst)" -ForegroundColor Green
}
