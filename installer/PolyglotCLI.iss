; installer/PolyglotCLI.iss
; Script de Inno Setup 7 para generar el instalador .exe de PolyglotCLI.
; Reemplaza al antiguo proyecto WiX MSI. Caracteristicas equivalentes:
;   - Instala el servidor web (artifacts/publish_out) en {app}\Server\serverapp
;   - Instala la aplicacion MAUI (artifacts/publish_maui) en {app}\Desktop\desktopapp
;   - Crea accesos directos en el Menu Inicio
;   - Acceso directo en el Escritorio opcional
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

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#APP_VERSION}
AppVerName={#MyAppName} {#APP_VERSION}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}

; {autopf} = C:\Program Files en maquinas de 64 bits
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

AllowNoIcons=yes
LicenseFile=license.txt

; Icono del instalador .exe generado
SetupIconFile=..\assets\icons\app.ico

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

; Nombre del archivo de salida: PolyglotCLI-<version>-x64-setup.exe
OutputBaseFilename=PolyglotCLI-{#APP_VERSION}-x64-setup
OutputDir=dist

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
BeveledLabel={#MyAppName} {#APP_VERSION} - Instalador creado con Inno Setup

[Files]
; Servidor Web (PolyglotCLI.web)
Source: "..\artifacts\publish_out\*"; \
    DestDir: "{app}\Server\serverapp"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Aplicacion de escritorio (PolyglotCLI.Maui)
Source: "..\artifacts\publish_maui\*"; \
    DestDir: "{app}\Desktop\desktopapp"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
; Iconos del Escritorio (opcional, sin marcar por defecto)
Name: "desktopicon"; \
    Description: "Crear accesos directos en el Escritorio"; \
    GroupDescription: "Accesos directos adicionales:"; \
    Flags: unchecked

[Icons]
; --- Menu Inicio (siempre) ---
Name: "{group}\PolyglotCLI Server"; \
    Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    WorkingDir: "{app}\Server\serverapp"; \
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Inicia el servidor web PolyglotCLI en el navegador"

Name: "{group}\PolyglotCLI Desktop"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    Comment: "Inicia la aplicacion nativa de escritorio PolyglotCLI"

; --- Escritorio (opcional via tarea desktopicon) ---
Name: "{commondesktop}\PolyglotCLI Server"; \
    Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    WorkingDir: "{app}\Server\serverapp"; \
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Inicia el servidor web PolyglotCLI"; \
    Tasks: desktopicon

Name: "{commondesktop}\PolyglotCLI Desktop"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    Comment: "Inicia la aplicacion de escritorio PolyglotCLI"; \
    Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{group}"

[Run]
; Lanzar el servidor al terminar la instalacion
Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Description: "Iniciar PolyglotCLI Server al finalizar"; \
    Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
// Valida que existan los artefactos publicados antes de continuar.
function InitializeSetup(): Boolean;
begin
  if not DirExists(ExpandConstant('{src}\..\artifacts\publish_out')) then
  begin
    MsgBox('No se encontró la carpeta "artifacts\publish_out".' + #13#10 +
           'Ejecute primero:' + #13#10 +
           '  dotnet publish PolyglotCLI.web/PolyglotCLI.web.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish_out',
      mbCriticalError, MB_OK);
    Result := False;
    exit;
  end;

  if not DirExists(ExpandConstant('{src}\..\artifacts\publish_maui')) then
  begin
    MsgBox('No se encontró la carpeta "artifacts\publish_maui".' + #13#10 +
           'Ejecute primero:' + #13#10 +
           '  dotnet publish PolyglotCLI.Maui/PolyglotCLI.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -o artifacts\publish_maui',
      mbCriticalError, MB_OK);
    Result := False;
    exit;
  end;

  Result := True;
end;
