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

; Imagenes del asistente tomadas del manifiesto grafico de la aplicacion
; (assets/msix/Assets). Si se sustituyen por otras imagenes, mantener las
; proporciones recomendadas:
;   WizardImageFile        ~ 164x314 px  (lateral del wizard, banda vertical)
;   WizardSmallImageFile   ~ 55x55  px   (icono de cabecera, esquina sup. izq.)
WizardImageFile=..\assets\msix\Assets\LogoSimple.png
WizardSmallImageFile=..\assets\msix\Assets\Square44x44Logo.png
WizardImageStretch=yes
WizardImageBackColor=$1F2937

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
; Servidor Web (PolyglotCLI.web)
Source: "..\artifacts\publish_out\*"; \
    DestDir: "{app}\Server\serverapp"; \
    Components: server; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Aplicacion de escritorio (PolyglotCLI.Maui)
Source: "..\artifacts\publish_maui\*"; \
    DestDir: "{app}\Desktop\desktopapp"; \
    Components: desktop; \
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
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Inicia el servidor web PolyglotCLI (se abre en http://localhost:5000)"; \
    Components: server

Name: "{group}\PolyglotCLI - Web"; \
    Filename: "{cmd}"; \
    Parameters: "/C start """" http://localhost:5000"; \
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Abre el panel web de PolyglotCLI en el navegador predeterminado"; \
    Components: server; Flags: runmaximized

Name: "{group}\PolyglotCLI - Desktop"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    Comment: "Inicia la aplicacion nativa de escritorio PolyglotCLI"; \
    Components: desktop

; --- Escritorio (siempre, filtrados por componente y por tarea desktopicon) ---
Name: "{commondesktop}\PolyglotCLI - Servidor Web"; \
    Filename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    WorkingDir: "{app}\Server\serverapp"; \
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Inicia el servidor web PolyglotCLI"; \
    Tasks: desktopicon; Components: server

Name: "{commondesktop}\PolyglotCLI - Abrir en el navegador"; \
    Filename: "{cmd}"; \
    Parameters: "/C start """" http://localhost:5000"; \
    IconFilename: "{app}\Server\serverapp\PolyglotCLI.exe"; \
    Comment: "Abre el panel web de PolyglotCLI en el navegador"; \
    Tasks: desktopicon; Components: server

Name: "{commondesktop}\PolyglotCLI - Escritorio nativo"; \
    Filename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    WorkingDir: "{app}\Desktop\desktopapp"; \
    IconFilename: "{app}\Desktop\desktopapp\PolyglotCLI.Maui.exe"; \
    Comment: "Inicia la aplicacion nativa de escritorio PolyglotCLI"; \
    Tasks: desktopicon; Components: desktop

[UninstallDelete]
Type: filesandordirs; Name: "{group}"

[Run]
; Ofrecer abrir el panel web en el navegador al finalizar la instalacion
; (solo si el componente server fue seleccionado). Por defecto desmarcado
; para no lanzar procesos automaticamente.
Filename: "{cmd}"; \
    Parameters: "/C start """" http://localhost:5000"; \
    Description: "Abrir PolyglotCLI en el navegador al finalizar"; \
    Components: server; \
    Flags: nowait postinstall skipifsilent runascurrentuser unchecked

; Nota: las validaciones de los artefactos publicados (artifacts/publish_out y
; artifacts/publish_maui) viven en scripts/build_installer.ps1, NO aqui.
; El instalador final empaqueta los archivos en su interior y los usuarios
; finales (descargados desde GitHub Releases o winget) no necesitan tener esas
; carpetas en su equipo.
[Code]
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
