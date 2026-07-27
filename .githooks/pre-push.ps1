# Pre-push hook: corre dotnet build + dotnet test antes de cada
# push. Si algo falla, bloquea el push para que veas el error antes
# de subir algo roto a master.
#
# El wrapper pre-push.bat llama a este script. La razón de tener
# dos archivos es que git en Windows no puede ejecutar archivos
# .ps1 directamente como hooks sin el wrapper, pero SI puede
# ejecutar .bat / .cmd.
#
# Configurar con:
#   git config core.hooksPath .githooks
#
# Para skipearlo en una emergencia:
#   git push --no-verify

$ErrorActionPreference = 'Stop'
$projectRoot = git rev-parse --show-toplevel
Set-Location $projectRoot

# Forzar output sin colores en dotnet: la salida de dotnet trae
# ANSI escapes (colores, cursor moves). Cuando PowerShell los
# re-imprime desde los temp files, interpreta secuencias como
# `\x1b[1EM` como comandos (M es alias de Move-Item) y termina
# escupiendo "EM no se reconoce como un comando" antes de que
# lleguemos al final del build.
$env:NO_COLOR = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Write-Host ""
Write-Host "==> pre-push: dotnet build..." -ForegroundColor Cyan

# Redirigimos a temp files en vez de capturar en strings. Eso
# deja que dotnet escriba directo a disco (sin pasar por el
# pipeline de PowerShell) y elimina cualquier interpretación
# intermedia de ANSI / control chars.
$buildLog = Join-Path $env:TEMP "polyglotcli-prepush-build.log"
$buildErr = Join-Path $env:TEMP "polyglotcli-prepush-build.err"

& dotnet build PolyglotCLI.slnx --no-incremental *>$buildLog 2>$buildErr
$buildExit = $LASTEXITCODE

# Mostrar output. NO_COLOR=1 arriba le pide a dotnet omitir
# los escapes ANSI, así que el contenido es texto plano UTF-8
# sin color. Lo leemos como string y lo escribimos al host.
if (Test-Path $buildLog) { Get-Content -LiteralPath $buildLog -Encoding UTF8 | Write-Host }
if (Test-Path $buildErr) { Get-Content -LiteralPath $buildErr -Encoding UTF8 | Write-Host }

if ($buildExit -ne 0) {
    Write-Host ""
    Write-Host "==> pre-push: build FAILED (exit $buildExit). Push bloqueado." -ForegroundColor Red
    Write-Host "Resolvé los errores y volvé a intentar, o usá 'git push --no-verify' para forzar." -ForegroundColor Yellow
    Remove-Item $buildLog, $buildErr -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "==> pre-push: build OK. Corriendo dotnet test..." -ForegroundColor Cyan

$testLog = Join-Path $env:TEMP "polyglotcli-prepush-test.log"
$testErr = Join-Path $env:TEMP "polyglotcli-prepush-test.err"

& dotnet test PolyglotCLI.test/PolyglotCLI.test.csproj --no-build *>$testLog 2>$testErr
$testExit = $LASTEXITCODE

if (Test-Path $testLog) { Get-Content -LiteralPath $testLog -Encoding UTF8 | Write-Host }
if (Test-Path $testErr) { Get-Content -LiteralPath $testErr -Encoding UTF8 | Write-Host }

Remove-Item $buildLog, $buildErr, $testLog, $testErr -ErrorAction SilentlyContinue

if ($testExit -ne 0) {
    Write-Host ""
    Write-Host "==> pre-push: tests FAILED (exit $testExit). Push bloqueado." -ForegroundColor Red
    Write-Host "Resolvé los tests y volvé a intentar, o usá 'git push --no-verify' para forzar." -ForegroundColor Yellow
    exit 1
}

Write-Host "==> pre-push: build + tests OK. Adelante con el push." -ForegroundColor Green
Write-Host ""
exit 0
