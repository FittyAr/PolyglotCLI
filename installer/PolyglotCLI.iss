; installer/PolyglotCLI.iss
; Script de Inno Setup 7 para generar el instalador .exe de PolyglotCLI.
;
; Durante la instalacion el usuario elige que modulos instalar:
;   - Servidor Web (PolyglotCLI Web/Server)  -> {app}\Server\serverapp
;   - Escritorio nativo (PolyglotCLI MAUI)   -> {app}\Desktop\desktopapp
; Por defecto ambos vienen marcados (Instalacion completa). En el modo
; Personalizado el usuario puede desmarcar uno o ambos (pero al menos uno
; debe quedar seleccionado, validado en NextButtonClick).
;
; Uso:
;   ISCC /DAPP_VERSION=<x.y.z> installer\PolyglotCLI.iss
; o desde PowerShell:
;   pwsh scripts\build_installer.ps1 -Version <x.y.z>

#define MyAppId          "3c1ef7e1-897d-411a-bc07-bc2cd4ad9d6f"
#define MyAppName        "PolyglotCLI"
#define MyAppPublisher   "FittyAr"
#define MyAppURL         "https://github.com/FittyAr/PolyglotCLI"
#ifndef APP_VERSION
  #define APP_VERSION    "1.1.0"
#endif

; Espacio minimo requerido (KB) para la pantalla "Seleccione la carpeta de
; destino". Las directivas [Files] usan Components:, por lo que Inno Setup
; NO las contabiliza automaticamente en ExtraDiskSpaceRequired. El valor
; se calcula en tiempo de build desde los artefactos publicados por
; scripts/build_installer.ps1 y se inyecta via /DEXTRA_SPACE_KB=N al
; invocar ISCC. El valor por defecto cubre la instalacion completa (server
; + desktop ~510 MB) y solo se usa cuando ISCC se ejecuta directamente.
#ifndef EXTRA_SPACE_KB
  #define EXTRA_SPACE_KB "532480"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#APP_VERSION}
AppVerName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}

; {autopf} = C:\Program Files en maquinas de 64 bits
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

AllowNoIcons=yes
LicenseFile=..\LICENSE

; Icono del instalador .exe generado
SetupIconFile=..\assets\icons\app.ico

; Imagenes del asistente generadas por scripts/generate_brand_assets.ps1 a
; partir del arte de marca (assets/brand). Se pasa una lista de tamanos con la
; proporcion exacta de Inno (164x314 y 55x55 al 100%, mas 125/150/200%):
; Setup elige la que mejor encaja segun DPI y WizardSizePercent, asi la
; imagen no se deforma (antes se estiraba un PNG cuadrado de 1024x1024).
WizardImageFile=..\assets\brand\installer\WizardImage-100.png,..\assets\brand\installer\WizardImage-125.png,..\assets\brand\installer\WizardImage-150.png,..\assets\brand\installer\WizardImage-200.png
WizardSmallImageFile=..\assets\brand\installer\WizardSmallImage-100.png,..\assets\brand\installer\WizardSmallImage-125.png,..\assets\brand\installer\WizardSmallImage-150.png,..\assets\brand\installer\WizardSmallImage-200.png
WizardImageStretch=no
; Mismo color que el fondo de WizardImage (#0F172A, formato $bbggrr).
WizardImageBackColor=$2A170F

; Compresion solida + LZMA2 para minimizar el tamano del instalador
Compression=lzma2/ultra64
SolidCompression=yes

; Instalador moderno con imagenes del manifiesto
WizardStyle=modern
WizardSizePercent=120

PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Icono del desinstalador tal y como aparece en "Programas y caracteristicas".
; Apuntamos al propio ejecutable del escritorio (que lleva app.ico embebido
; gracias a <ApplicationIcon> en PolyglotCLI.Maui.csproj) para mantener una
; unica fuente de verdad del icono corporativo.
UninstallDisplayIcon={app}\app.ico

; Nombre en "Programas y caracteristicas" sin la version concatenada (la
; version ya tiene su propia columna, tomada de AppVersion; Publisher y
; HelpLink salen de AppPublisher/AppSupportURL). Sustituye las antiguas
; escrituras manuales en HKLM\...\Uninstall, que fallaban cuando el usuario
; elegia instalar "solo para mi" (modo sin privilegios de administrador).
UninstallDisplayName={#MyAppName}

; Espacio minimo requerido en la pantalla de seleccion de carpeta. Ver el
; comentario sobre EXTRA_SPACE_KB al inicio del script.
ExtraDiskSpaceRequired={#EXTRA_SPACE_KB}

; Nombre del archivo de salida: PolyglotCLI-<version>-x64-setup.exe
OutputBaseFilename=PolyglotCLI-{#APP_VERSION}-x64-setup
OutputDir=..\artifacts\dist

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
BeveledLabel={#MyAppName} {#APP_VERSION}

; Tipos de instalacion. "full" deja todo marcado; "custom" muestra la pantalla
; de componentes para que el usuario marque/desmarque individualmente.
[Types]
Name: "full";     Description: "Instalacion completa (Servidor Web + Escritorio nativo)"
Name: "custom";   Description: "Personalizada"; Flags: iscustom

; Componentes seleccionables durante la instalacion.
[Components]
Name: "server";   Description: "Servidor Web (PolyglotCLI Web)";   Types: full custom
Name: "desktop";  Description: "Escritorio nativo (PolyglotCLI MAUI)"; Types: full custom

[Files]
; Icono de la aplicacion compartido para todos los accesos directos
Source: "..\assets\icons\app.ico"; DestDir: "{app}"; Flags: ignoreversion

; Servidor Web (PolyglotCLI.web)
Source: "..\artifacts\publish_out\*"; \
    DestDir: "{app}\Server\serverapp"; \
    Components: server; Excludes: "*.pdb"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Aplicacion de escritorio (PolyglotCLI.Maui)
Source: "..\artifacts\publish_maui\*"; \
    DestDir: "{app}\Desktop\desktopapp"; \
    Components: desktop; Excludes: "*.pdb"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
; Crear accesos directos en el Escritorio de Windows para los componentes
; instalados. Marcado por defecto; el usuario puede desmarcarlo si lo desea.
Name: "desktopicon"; \
    Description: "Crear accesos directos en el Escritorio"; \
    GroupDescription: "Accesos directos adicionales:"

[Icons]
; --- Menu Inicio (siempre, filtrados por componente) ---
Name: "{group}\PolyglotCLI - Server"; \
    Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    WorkingDir: "{app}\Server\serverapp"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Inicia el servidor web PolyglotCLI (se abre en http://localhost:5000)"; \
    Components: server

Name: "{group}\PolyglotCLI - Web"; \
    Filename: "{cmd}"; \
    Parameters: "/C start """" http://localhost:5000"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Abre el panel web de PolyglotCLI en el navegador predeterminado"; \
    Components: server; Flags: runmaximized

Name: "{group}\PolyglotCLI - Desktop"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Inicia la aplicacion nativa de escritorio PolyglotCLI"; \
    Components: desktop

; --- Escritorio (siempre, filtrados por componente y por tarea desktopicon) ---
Name: "{autodesktop}\PolyglotCLI - Server"; \
    Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    WorkingDir: "{app}\Server\serverapp"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Inicia el servidor web PolyglotCLI"; \
    Tasks: desktopicon; Components: server

Name: "{autodesktop}\PolyglotCLI - Web"; \
    Filename: "{cmd}"; \
    Parameters: "/C start """" http://localhost:5000"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Abre el panel web de PolyglotCLI en el navegador"; \
    Tasks: desktopicon; Components: server

Name: "{autodesktop}\PolyglotCLI - Desktop"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\app.ico"; \
    Comment: "Inicia la aplicacion nativa de escritorio PolyglotCLI"; \
    Tasks: desktopicon; Components: desktop

[UninstallDelete]
Type: filesandordirs; Name: "{group}"

; Nota: las validaciones de los artefactos publicados (artifacts/publish_out y
; artifacts/publish_maui) viven en scripts/build_installer.ps1, NO aqui.
; El instalador final empaqueta los archivos en su interior y los usuarios
; finales (descargados desde GitHub Releases o winget) no necesitan tener esas
; carpetas en su equipo.

; Nota: se ha eliminado la seccion [Run] deliberadamente. No se ofrece
; lanzar el servidor web ni abrir el navegador al finalizar la instalacion.

[Code]

// ---------------------------------------------------------------------------
// Runtimes de .NET 11 por componente:
//   - Escritorio (MAUI, framework-dependent): su runtimeconfig.json solo
//     declara Microsoft.NETCore.App 11.* (Windows App SDK va incluido en la
//     carpeta de la app). Si falta, se instala el .NET 11 Desktop Runtime,
//     que lo incluye.
//   - Servidor Web (ASP.NET Core): Microsoft.NETCore.App 11.* +
//     Microsoft.AspNetCore.App 11.*.
// La verificacion se hace en PrepareToInstall (despues de elegir componentes,
// tambien en modo silencioso) y solo para los componentes seleccionados.
// ---------------------------------------------------------------------------

function IsSharedRuntimeInstalled(const FrameworkName: string): Boolean;
var
  SearchRec: TFindRec;
  FileName: string;
  Output: AnsiString;
  ResultCode: Integer;
begin
  Result := False;

  // 1. Verificacion directa en la carpeta de runtimes compartidos (x64).
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\') + FrameworkName + '\11.*', SearchRec) then
  begin
    FindClose(SearchRec);
    Result := True;
    Exit;
  end;

  // 2. Consulta via 'dotnet --list-runtimes' (instalaciones en otra ruta).
  FileName := ExpandConstant('{tmp}\dotnet_runtimes_check.txt');
  if Exec(ExpandConstant('{cmd}'), '/C dotnet --list-runtimes > "' + FileName + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(FileName, Output) then
      Result := Pos(FrameworkName + ' 11.', string(Output)) > 0;
    DeleteFile(FileName);
  end;
end;

function InstallSharedRuntime(const FrameworkName, DisplayName, WingetId, DownloadUrl, LocalFileName: string): Boolean;
var
  ResultCode: Integer;
  InstallerPath: string;
begin
  Result := False;

  // 1. winget (Windows 10/11). Si no esta disponible, el comando falla sin mas.
  Exec(ExpandConstant('{cmd}'), '/C winget install --id ' + WingetId + ' --exact --silent --accept-package-agreements --accept-source-agreements', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if IsSharedRuntimeInstalled(FrameworkName) then
  begin
    Result := True;
    Exit;
  end;

  // 2. Descarga directa del instalador oficial de Microsoft.
  InstallerPath := ExpandConstant('{tmp}\') + LocalFileName;
  try
    DownloadTemporaryFile(DownloadUrl, LocalFileName, '', nil);
    if FileExists(InstallerPath) then
    begin
      if Exec(InstallerPath, '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
        Result := IsSharedRuntimeInstalled(FrameworkName) or (ResultCode = 0) or (ResultCode = 3010);
    end;
  except
    SuppressibleMsgBox('No se pudo descargar automáticamente ' + DisplayName + ': ' + GetExceptionMessage + #13#10 +
      'Descárguelo e instálelo manualmente desde https://dotnet.microsoft.com/download/dotnet/11.0',
      mbError, MB_OK, IDOK);
  end;
end;

// Devuelve '' si se puede continuar, o un mensaje que aborta la instalacion.
function EnsureRuntime(const ComponentLabel, FrameworkName, DisplayName, WingetId, DownloadUrl, LocalFileName: string): String;
begin
  Result := '';
  if IsSharedRuntimeInstalled(FrameworkName) then
    Exit;

  if SuppressibleMsgBox(
       'El componente "' + ComponentLabel + '" requiere ' + DisplayName + ' y no se ha detectado en su sistema.' + #13#10 + #13#10 +
       '¿Desea descargarlo e instalarlo automáticamente ahora?',
       mbConfirmation, MB_YESNO, IDYES) = IDYES then
  begin
    if InstallSharedRuntime(FrameworkName, DisplayName, WingetId, DownloadUrl, LocalFileName) then
      Exit;
  end;

  if SuppressibleMsgBox(
       'No se pudo confirmar la instalación de ' + DisplayName + '.' + #13#10 +
       'Sin él, "' + ComponentLabel + '" no arrancará.' + #13#10 + #13#10 +
       '¿Desea continuar con la instalación de PolyglotCLI de todos modos?',
       mbConfirmation, MB_YESNO, IDYES) = IDNO then
    Result := DisplayName + ' es necesario para "' + ComponentLabel + '". ' +
      'Instálelo desde https://dotnet.microsoft.com/download/dotnet/11.0 y vuelva a ejecutar el instalador.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if WizardIsComponentSelected('desktop') then
  begin
    Result := EnsureRuntime('Escritorio nativo (PolyglotCLI MAUI)',
      'Microsoft.NETCore.App', '.NET 11 Desktop Runtime',
      'Microsoft.DotNet.DesktopRuntime.11',
      'https://aka.ms/dotnet/11.0/windowsdesktop-runtime-win-x64.exe',
      'dotnet-desktop-runtime-11-win-x64.exe');
    if Result <> '' then
      Exit;
  end;

  if WizardIsComponentSelected('server') then
  begin
    Result := EnsureRuntime('Servidor Web (PolyglotCLI Web)',
      'Microsoft.NETCore.App', '.NET 11 Runtime',
      'Microsoft.DotNet.Runtime.11',
      'https://aka.ms/dotnet/11.0/dotnet-runtime-win-x64.exe',
      'dotnet-runtime-11-win-x64.exe');
    if Result <> '' then
      Exit;

    Result := EnsureRuntime('Servidor Web (PolyglotCLI Web)',
      'Microsoft.AspNetCore.App', 'ASP.NET Core 11 Runtime',
      'Microsoft.DotNet.AspNetCore.11',
      'https://aka.ms/dotnet/11.0/aspnetcore-runtime-win-x64.exe',
      'aspnetcore-runtime-11-win-x64.exe');
  end;
end;

// Garantiza que el usuario seleccione al menos un componente antes de
// pasar de la pagina de seleccion. Sin esta validacion es posible
// "instalar" PolyglotCLI sin instalar nada util.
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  if CurPageID = wpSelectComponents then
  begin
    if (not WizardIsComponentSelected('server')) and
       (not WizardIsComponentSelected('desktop')) then
    begin
      MsgBox(
        'Debe seleccionar al menos un componente para instalar:' + #13#10 +
        '  - Servidor Web (PolyglotCLI Web)' + #13#10 +
        '  - Escritorio nativo (PolyglotCLI MAUI)',
        mbError, MB_OK);
      Result := False;
    end
    else
      Result := True;
  end
  else
    Result := True;
end;
