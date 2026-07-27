@echo off
REM Wrapper de pre-push: llama al script PowerShell que corre
REM dotnet build + dotnet test antes de cada push.
REM
REM PowerShell en Windows rechaza archivos sin extension .ps1
REM cuando se invoca con -File, por eso este wrapper .bat existe.
REM Git para Windows puede ejecutar .bat / .cmd como hooks sin
REM problema; en Unix el .bat es ignorado (y el .ps1 tampoco
REM funcionaría sin pwsh, asi que este hook es Windows-only).

pushd "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0pre-push.ps1"
set EXITCODE=%ERRORLEVEL%
popd
exit /b %EXITCODE%
