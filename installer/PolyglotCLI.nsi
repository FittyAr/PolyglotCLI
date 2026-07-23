; installer/PolyglotCLI.nsi
; Script de Nullsoft Install System (NSIS 3) para generar el instalador .exe de
; PolyglotCLI. Es el equivalente al script de Inno Setup
; (installer/PolyglotCLI.iss) y produce un instalador con la misma jerarquia
; de carpetas, accesos directos, configuracion de registro y aspecto grafico
; en espanol.
;
; Uso:
;   makensis /DAPP_VERSION=<x.y.z> /DEXTRA_SPACE_KB=N installer\PolyglotCLI.nsi
; o desde PowerShell:
;   pwsh scripts\build_nsis_installer.ps1 -Version <x.y.z>

Unicode True

; ============================================================================
; Constantes que pueden sobreescribirse desde la linea de comandos de makensis
; mediante /DAPP_VERSION=x.y.z y /DEXTRA_SPACE_KB=N.
; ============================================================================
!ifndef APP_VERSION
  !define APP_VERSION "1.1.0"
!endif

!ifndef EXTRA_SPACE_KB
  ; Valor por defecto conservador que cubre la instalacion completa
  ; (server + desktop ~510 MB). Solo se usa cuando makensis se invoca sin
  ; inyectar el tamano real calculado por scripts/build_nsis_installer.ps1.
  !define EXTRA_SPACE_KB 532480
!endif

; Identificadores y metadatos
!define APP_ID          "PolyglotCLI"
!define APP_NAME        "PolyglotCLI"
!define APP_PUBLISHER   "FittyAr"
!define APP_URL         "https://github.com/FittyAr/PolyglotCLI"
!define APP_EXE_NAME    "PolyglotCLI.exe"
!define APP_MAUI_NAME   "PolyglotCLI.Maui.exe"

; ============================================================================
; Includes del Modern UI 2 y soporte multi-usuario. Las defines de
; MULTIUSER_* deben declararse ANTES de incluir MultiUser.nsh; las de MUI
; requieren que MUI2.nsh este cargada.
; ============================================================================
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

; ============================================================================
; Privilegios y soporte multi-usuario (por maquina o por usuario). Replicamos
; PrivilegesRequired=admin de Inno Setup exigiendo elevacion para escribir en
; Program Files. La eleccion entre "Todos los usuarios" o "Solo este usuario"
; se ofrece en una pagina dedicada del MUI. MULTIUSER_EXECUTIONLEVEL debe
; definirse ANTES de incluir MultiUser.nsh.
; ============================================================================
!define MULTIUSER_EXECUTIONLEVEL Admin
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallMode"
!define MULTIUSER_INSTALLMODE_INSTDIR "${APP_NAME}"
!include "MultiUser.nsh"

; ============================================================================
; Configuracion del instalador
; ============================================================================
Name "${APP_NAME} ${APP_VERSION}"
OutFile "..\artifacts\dist\PolyglotCLI-${APP_VERSION}-x64-nsis-setup.exe"
InstallDir "$PROGRAMFILES64\${APP_PUBLISHER}\${APP_NAME}"
InstallDirRegKey HKLM "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}" "InstallDir"
ShowInstDetails show
ShowUninstDetails show
BrandingText "${APP_NAME} ${APP_VERSION} - ${APP_PUBLISHER}"

; Icono del instalador .exe generado
Icon "..\assets\icons\app.ico"
UninstallIcon "..\assets\icons\app.ico"

; Compresion solida + LZMA para minimizar el tamano del instalador
SetCompress auto
SetCompressor /SOLID lzma
SetCompressorDictSize 64

; ============================================================================
; Privilegios y soporte multi-usuario. Las defines se declararon arriba, antes
; de !include "MultiUser.nsh".
; ============================================================================

; ============================================================================
; Configuracion visual del Modern UI 2 (espanol). Las imagenes se generan en
; assets/nsis/ a partir de los PNG del manifiesto (assets/msix/Assets).
; ============================================================================
!define MUI_ABORTWARNING
!define MUI_ABORTWARNING_TEXT "Desea cancelar la instalacion de ${APP_NAME}?"
!define MUI_ICON   "..\assets\icons\app.ico"
!define MUI_UNICON "..\assets\icons\app.ico"

!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP     "..\assets\nsis\Header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP   "..\assets\nsis\UnHeader.bmp"
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADER_TRANSPARENT_TEXT
!define MUI_BGCOLOR "1F2937"
!define MUI_TEXTCOLOR "FFFFFF"

!define MUI_WELCOMEFINISHPAGE_BITMAP    "..\assets\nsis\Wizard.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP  "..\assets\nsis\UnWizard.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP_NOSTRETCH

; ============================================================================
; Paginas del instalador
; ============================================================================
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateComponentSelection
!define MUI_PAGE_CUSTOMFUNCTION_PRE DisableDesktopTaskIfNoDesktop

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; ============================================================================
; Idiomas (primario espanol, ingles como fallback)
; ============================================================================
!insertmacro MUI_LANGUAGE "Spanish"
!insertmacro MUI_LANGUAGE "English"

; Descripciones localizadas de los componentes
LangString DESC_SectionServer  ${LANG_SPANISH} "Instala el servidor web Blazor de PolyglotCLI (PolyglotCLI.exe) y crea los accesos directos en el Menu Inicio."
LangString DESC_SectionDesktop ${LANG_SPANISH} "Instala la aplicacion nativa de escritorio (PolyglotCLI.Maui.exe) y crea el acceso directo en el Menu Inicio."
LangString DESC_SectionDesktopIcon ${LANG_SPANISH} "Ademas de los accesos directos del Menu Inicio, crea accesos directos en el Escritorio de Windows."
LangString DESC_InstModeTitle   ${LANG_SPANISH} "Modo de instalacion"
LangString DESC_InstModeSub     ${LANG_SPANISH} "Elija si ${APP_NAME} debe instalarse para todos los usuarios o solo para el usuario actual."

LangString DESC_SectionServer  ${LANG_ENGLISH} "Installs the PolyglotCLI Blazor web server (PolyglotCLI.exe) and creates the Start Menu shortcuts."
LangString DESC_SectionDesktop ${LANG_ENGLISH} "Installs the native desktop application (PolyglotCLI.Maui.exe) and creates the Start Menu shortcut."
LangString DESC_SectionDesktopIcon ${LANG_ENGLISH} "In addition to the Start Menu shortcuts, creates shortcuts on the Windows Desktop."
LangString DESC_InstModeTitle   ${LANG_ENGLISH} "Installation mode"
LangString DESC_InstModeSub     ${LANG_ENGLISH} "Choose whether ${APP_NAME} should be installed for all users or only for the current user."

; ============================================================================
; Tipos de instalacion (full = completa, custom = personalizada).
; ============================================================================
InstType "$(^Full)"
InstType "$(^Custom)"

; ============================================================================
; Secciones (componentes). Ambas vienen marcadas por defecto; el usuario puede
; desmarcarlas en el modo "Personalizada" pero al menos una debe quedar
; seleccionada (validado en ValidateComponentSelection).
; ============================================================================
Section "-app.ico" SEC_AppIco
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "..\assets\icons\app.ico"
SectionEnd

Section "Servidor Web (PolyglotCLI Web)" SEC_Server
  SectionIn 1 2
  AddSize ${EXTRA_SPACE_KB}
  SetOutPath "$INSTDIR\Server\serverapp"
  File /r "..\artifacts\publish_out\*.*"
SectionEnd

Section "Escritorio nativo (PolyglotCLI MAUI)" SEC_Desktop
  SectionIn 1 2
  AddSize ${EXTRA_SPACE_KB}
  SetOutPath "$INSTDIR\Desktop\desktopapp"
  File /r "..\artifacts\publish_maui\*.*"
SectionEnd

Section /o "Crear accesos directos en el Escritorio" SEC_DesktopIcon
  SectionIn 1 2
SectionEnd

Section "-uninst" SEC_UninstSection
  SectionIn RO
  WriteUninstaller "$INSTDIR\Uninst.exe"
SectionEnd

; Descripciones de los componentes (MUI)
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_Server}     $(DESC_SectionServer)
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_Desktop}    $(DESC_SectionDesktop)
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DesktopIcon} $(DESC_SectionDesktopIcon)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ============================================================================
; Accesos directos. Se crean en la fase de instalacion siguiendo la eleccion
; del usuario en el modo multi-usuario (SetShellVarContext se ajusta solo).
; ============================================================================
Section "-shortcuts" SEC_Shortcuts
  ; Construye la ruta base del Menu Inicio en funcion del contexto
  ; (todos los usuarios / usuario actual) y del nombre del grupo.
  StrCpy $0 "$SMPROGRAMS\${APP_NAME}"
  CreateDirectory "$0"

  SectionGetFlags ${SEC_Server} $1
  IntOp $1 $1 & ${SF_SELECTED}
  ${If} $1 == ${SF_SELECTED}
    CreateShortcut "$0\PolyglotCLI - Server.lnk" "$INSTDIR\Server\serverapp\${APP_EXE_NAME}" "" "$INSTDIR\app.ico"
    CreateShortcut "$0\PolyglotCLI - Web.lnk" "$SYSDIR\cmd.exe" '/C start "" http://localhost:5000' "$INSTDIR\app.ico"
  ${EndIf}

  SectionGetFlags ${SEC_Desktop} $1
  IntOp $1 $1 & ${SF_SELECTED}
  ${If} $1 == ${SF_SELECTED}
    CreateShortcut "$0\PolyglotCLI - Desktop.lnk" "$INSTDIR\Desktop\desktopapp\${APP_MAUI_NAME}" "" "$INSTDIR\app.ico"
  ${EndIf}

  SectionGetFlags ${SEC_DesktopIcon} $1
  IntOp $1 $1 & ${SF_SELECTED}
  ${If} $1 == ${SF_SELECTED}
    SectionGetFlags ${SEC_Server} $2
    IntOp $2 $2 & ${SF_SELECTED}
    ${If} $2 == ${SF_SELECTED}
      CreateShortcut "$DESKTOP\PolyglotCLI - Server.lnk" "$INSTDIR\Server\serverapp\${APP_EXE_NAME}" "" "$INSTDIR\app.ico"
      CreateShortcut "$DESKTOP\PolyglotCLI - Web.lnk" "$SYSDIR\cmd.exe" '/C start "" http://localhost:5000' "$INSTDIR\app.ico"
    ${EndIf}

    SectionGetFlags ${SEC_Desktop} $2
    IntOp $2 $2 & ${SF_SELECTED}
    ${If} $2 == ${SF_SELECTED}
      CreateShortcut "$DESKTOP\PolyglotCLI - Desktop.lnk" "$INSTDIR\Desktop\desktopapp\${APP_MAUI_NAME}" "" "$INSTDIR\app.ico"
    ${EndIf}
  ${EndIf}
SectionEnd

; ============================================================================
; Registro: clave comun HKLM\SOFTWARE\FittyAr\PolyglotCLI con InstallDir y
; UninstallString. Ademas, las claves estandar que lee "Programas y
; caracteristicas" para que muestre el nombre, version, publisher y tamano
; correctos. Las escrituras en HKLM se realizan solo cuando la instalacion es
; por maquina; en modo por usuario se crean en HKCU.
; ============================================================================
Section "-registry" SEC_Registry
  ; Contexto del shell: todos los usuarios o usuario actual
  ${If} $MultiUser.InstallMode == "AllUsers"
    WriteRegStr HKLM "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}" "InstallMode" "AllUsers"

    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName"     "${APP_NAME}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout"    "${APP_URL}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "HelpLink"        "${APP_URL}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninst.exe"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon"     "$INSTDIR\app.ico"

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $3 "0x%08X" $0
    WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "EstimatedSize" "$3"
  ${Else}
    WriteRegStr HKCU "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}" "InstallMode" "CurrentUser"

    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName"     "${APP_NAME}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout"    "${APP_URL}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "HelpLink"        "${APP_URL}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninst.exe"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon"     "$INSTDIR\app.ico"

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $3 "0x%08X" $0
    WriteRegDWORD HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "EstimatedSize" "$3"
  ${EndIf}
SectionEnd

; ============================================================================
; Callbacks
; ============================================================================
Function .onInit
  !insertmacro MULTIUSER_INIT

  ; Si el usuario pasa la propiedad APP_VERSION por linea de comandos, NSIS la
  ; evalua en tiempo de compilacion (define), por lo que Name/OutFile ya la
  ; reflejan. Aqui solo aseguramos que la UI de MUI este en espanol por
  ; defecto si el sistema lo soporta.
  ${If} ${Silent}
    ; Sin intervencion en modo silencioso
  ${Else}
    ; Forzar espanol como idioma principal cuando este disponible
    ${If} ${LANG_SPANISH} >= 0
      ; No-op: el orden de insercion de idiomas ya prioriza espanol.
    ${EndIf}
  ${EndIf}
FunctionEnd

Function un.onInit
  !insertmacro MULTIUSER_UNINIT
FunctionEnd

; Garantiza que el usuario deje al menos un componente (server o desktop)
; marcado antes de avanzar desde la pagina de seleccion. Sin esta validacion
; seria posible "instalar" PolyglotCLI sin instalar nada util.
Function ValidateComponentSelection
  SectionGetFlags ${SEC_Server} $0
  SectionGetFlags ${SEC_Desktop} $1
  IntOp $0 $0 & ${SF_SELECTED}
  IntOp $1 $1 & ${SF_SELECTED}

  ${If} $0 != ${SF_SELECTED}
  ${AndIf} $1 != ${SF_SELECTED}
    MessageBox MB_ICONSTOP|MB_OK \
      "Debe seleccionar al menos un componente para instalar:$\r$\n  - Servidor Web (PolyglotCLI Web)$\r$\n  - Escritorio nativo (PolyglotCLI MAUI)"
    Abort
  ${EndIf}
FunctionEnd

; Compatibilidad: en versiones antiguas de NSIS ${AndIf} requiere LogicLib.nsh,
; ya incluido arriba.

; Si el usuario desmarca el componente de Escritorio, desmarca automaticamente
; la tarea que crea accesos directos en el Escritorio (no tendria sentido
; activar el checkbox si no hay Desktop instalado).
Function DisableDesktopTaskIfNoDesktop
  SectionGetFlags ${SEC_Desktop} $0
  IntOp $0 $0 & ${SF_SELECTED}

  ${If} $0 != ${SF_SELECTED}
    SectionGetFlags ${SEC_DesktopIcon} $1
    IntOp $1 $1 | ${SF_RO}
    IntOp $1 $1 & ${SF_SELECTED}
    SectionSetFlags ${SEC_DesktopIcon} $1
    SectionSetText ${SEC_DesktopIcon} ""
  ${Else}
    SectionSetText ${SEC_DesktopIcon} "Crear accesos directos en el Escritorio"
  ${EndIf}
FunctionEnd

; ============================================================================
; Seccion de desinstalacion
; ============================================================================
Section "Uninstall"
  ; Eliminar accesos directos
  Delete "$SMPROGRAMS\${APP_NAME}\PolyglotCLI - Server.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\PolyglotCLI - Web.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\PolyglotCLI - Desktop.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  Delete "$DESKTOP\PolyglotCLI - Server.lnk"
  Delete "$DESKTOP\PolyglotCLI - Web.lnk"
  Delete "$DESKTOP\PolyglotCLI - Desktop.lnk"

  ; Eliminar contenido instalado
  RMDir /r "$INSTDIR\Server"
  RMDir /r "$INSTDIR\Desktop"
  Delete  "$INSTDIR\app.ico"
  Delete  "$INSTDIR\Uninst.exe"
  RMDir  "$INSTDIR"

  ; Eliminar entradas de registro en funcion del contexto
  ${If} $MultiUser.InstallMode == "AllUsers"
    DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegKey HKLM "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}"
  ${Else}
    DeleteRegKey HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegKey HKCU "SOFTWARE\${APP_PUBLISHER}\${APP_NAME}"
  ${EndIf}

  SetAutoClose true
SectionEnd