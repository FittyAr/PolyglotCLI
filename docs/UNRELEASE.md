# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Nuevo sistema de instalación basado en **Inno Setup 7** (`installer/PolyglotCLI.iss`, `installer/license.txt`, `scripts/build_installer.ps1`). Genera un instalador `.exe` con asistente gráfico nativo de Windows que reemplaza al antiguo `.msi` de WiX. Reutiliza los recursos del manifiesto (`assets/icons/app.ico`, `assets/msix/`) y la licencia MIT (`installer/license.txt`).

### Improved

- Workflow de release (`.github/workflows/release.yml`): el paso `Build MSI Installer (WiX)` se elimina. En su lugar se añade un paso `Build Setup EXE Installer (Inno Setup)` que invoca `pwsh scripts/build_installer.ps1 -Version <x.y.z> -NoPublish`. Los artefactos subidos a GitHub Release pasan de `PolyglotCLI-*.msi` a `installer/dist/PolyglotCLI-*-x64-setup.exe`.
- `scripts/install.ps1` (opción 7 de `run.ps1`): publica y compila el instalador Inno Setup, y luego lo ejecuta de forma interactiva. La desinstalación busca `%ProgramFiles%\FittyAr\PolyglotCLI\unins000.exe` (desinstalador nativo de Inno Setup) y, como respaldo, re-ejecuta el `.exe` con `/UNINSTALL`.
- `scripts/bump_version.ps1` (opción 6): la sección 5 ya no compila el proyecto WiX; en su lugar invoca `scripts/build_installer.ps1 -Version <x.y.z>` para validar la release construyendo el instalador Inno Setup end-to-end.

### Changed

- **Reorganización profesional del árbol de carpetas del repositorio**. Las carpetas sueltas del root se consolidan en ubicaciones canónicas:
  - `prompts/` → `assets/prompts/` (prompts del sistema LLM).
  - `manifests/app.ico` → `assets/icons/app.ico` (icono oficial de la aplicación).
  - `manifests/msix/` → `assets/msix/` (manifiesto y assets del paquete MSIX).
  - `installer/build_installer/` → `installer/dist/` (salida compilada del instalador Inno Setup).
  - `publish_out/` y `publish_maui/` → `artifacts/publish_out/` y `artifacts/publish_maui/` (outputs de `dotnet publish`, ahora ignorados por Git bajo `artifacts/`).
- `installer/PolyglotCLI.iss`: actualizadas las rutas de los artefactos publicados (`..\artifacts\publish_out`, `..\artifacts\publish_maui`) y del icono (`..\assets\icons\app.ico`). El `OutputDir` pasa de `build_installer` a `dist`. Los mensajes de error del asistente ahora mencionan la nueva ruta `artifacts\publish_out`.
- **Selector de componentes en el instalador**. El usuario elige qué módulos instalar durante el asistente:
  - `[Types]`: `full` (Instalación completa) y `custom` (Personalizada).
  - `[Components]`: `server` (Servidor Web) y `desktop` (Escritorio nativo MAUI). Ambos vienen marcados por defecto en `full` y son elegibles individualmente en `custom`.
  - `[Code] NextButtonClick`: impide avanzar de la página de selección si el usuario desmarca ambos componentes (debe quedar al menos uno).
- **Personalización visual del instalador con `assets/`**. El asistente ahora usa los recursos gráficos del manifiesto: `WizardImageFile = assets/msix/Assets/LogoSimple.png` (lateral) y `WizardSmallImageFile = assets/msix/Assets/Square44x44Logo.png` (cabecera), con `WizardImageStretch=yes` y fondo `$1F2937`.
- **Accesos directos renombrados y agrupados**. Cada componente expone accesos directos claros en el Menú Inicio (y Escritorio cuando se marca la tarea `desktopicon`, ahora activada por defecto):
  - `PolyglotCLI - Servidor Web` → arranca `Server\serverapp\PolyglotCLI.exe`.
  - `PolyglotCLI - Abrir en el navegador` → abre `http://localhost:5000` con el navegador predeterminado (atajo a `{cmd} /C start "" http://localhost:5000`, usando el icono del propio ejecutable del servidor).
  - `PolyglotCLI - Escritorio nativo` → arranca `Desktop\desktopapp\PolyglotCLI.Maui.exe`.
- **Idioma del instalador**: se sustituye `compiler:Default.isl` (inglés) por `compiler:Languages\Spanish.isl`, de modo que todos los textos del asistente aparecen en español.
- `[Run]` post-instalación: en lugar de lanzar el `.exe` del servidor automáticamente, ahora ofrece al usuario (desmarcado por defecto) abrir el panel web en el navegador. *Revertido en esta iteración: la sección `[Run]` se elimina por completo para no mostrar ningún checkbox al final del asistente ni lanzar procesos automáticamente.*
- `scripts/build_installer.ps1`, `scripts/install.ps1` y `.github/workflows/release.yml`: actualizados para publicar y leer los artefactos desde `artifacts/` y para generar el `.exe` final en `installer/dist/`.
- `PolyglotCLI.core/Services/PromptLoader.cs`: la resolución de rutas busca primero `assets/prompts/` (ubicación canónica) y conserva `prompts/` como ubicación de fallback para despliegues existentes.
- `PolyglotCLI.web/Components/Config/PromptsConfigTab.razor`: el texto informativo ahora indica `assets/prompts/` como carpeta de guardado.
- `README.md`, `.agents/AGENTS.md` y `.agents/skills/prompts-helper/SKILL.md`: actualizadas las referencias documentales para reflejar la nueva estructura de carpetas.

### Deprecated

### Removed

- Proyecto `PolyglotCLI.Wix/` (con sus `.wxs`, `.wxl`, `.wixproj` y carpetas `bin/`/`obj/`): se retira por completo del repositorio. `PolyglotCLI.slnx` deja de incluir `PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj`. Toda la lógica de MSI queda sustituida por el script Inno Setup mencionado arriba.
- Carpeta `manifests/` del root: se elimina por completo. Su contenido se reparte entre `assets/icons/`, `assets/msix/` y `installer/license.txt` (los recursos `installer_banner.png`, `installer_dialog.png` y `license.rtf` estaban huérfanos sin uso real en el código actual).

### Fixed

- **Error del instalador Inno Setup al no encontrar la carpeta `publish_out`**. El script `installer/PolyglotCLI.iss` validaba contra rutas relativas obsoletas; tras mover los outputs de `dotnet publish` a `artifacts/publish_out` y `artifacts/publish_maui`, los chequeos `InitializeSetup()` y las directivas `[Files]` apuntan a las nuevas rutas, eliminando el cuadro de error "No se encontró la carpeta publish_out" que aparecía al ejecutar ISCC directamente sin haber publicado antes.
- **El instalador .exe mostraba "No se encontró la carpeta artifacts\publish_out" a los usuarios finales** descargados desde GitHub Releases o winget. La validación `InitializeSetup()` miraba paths del workspace de desarrollo que no existen en el equipo del usuario. Se elimina completamente del `.iss` (los archivos ya vienen empaquetados dentro del `.exe` y la directiva `[Files]` los extrae desde su propio contenido). La validación se traslada a `scripts/build_installer.ps1`, que es donde tiene sentido ejecutarla (solo la ve el desarrollador, justo antes de invocar ISCC).
- **Iconos genéricos en los accesos directos del Escritorio, del Menú Inicio y en la entrada de "Programas y características" tras instalar.** Los `.csproj` de `PolyglotCLI.Maui` y `PolyglotCLI.web` no declaraban `<ApplicationIcon>`, por lo que los ejecutables compilados (`PolyglotCLI.Maui.exe` y `PolyglotCLI.exe`) no tenían un icono Win32 embebido y las directivas `IconFilename=` de Inno Setup caían en el icono genérico del sistema. Se añade `<ApplicationIcon>..\assets\icons\app.ico</ApplicationIcon>` en ambos `.csproj` como fuente única de verdad del icono. Además se añade `UninstallDisplayIcon={app}\Desktop\desktopapp\PolyglotCLI.Maui.exe` en `[Setup]` para que la entrada de "Programas y características" muestre el icono del propio ejecutable (que ahora lo lleva embebido).
- **Ventana vacía en el acceso directo "Escritorio nativo" tras instalar.** El host de WebView2 sobre WinUI 3 con `WindowsPackageType=None` no resolvía correctamente el host page del `BlazorWebView` cuando el ejecutable se servía sin recurso de icono Win32 desde una ruta bajo `Program Files`. Embeber `app.ico` como `ApplicationIcon` restaura el manifest de recursos del `.exe` y el `BlazorWebView` vuelve a localizar `wwwroot/index.html` y los assets estáticos publicados (`_content/Radzen.Blazor`, `_content/Cropper.Blazor`, `lib/bootstrap`, `app.css`, `PolyglotCLI.Maui.styles.css`, `_framework/blazor.webview.js`).
- **La columna "Nombre" de "Programas y características" mostraba "PolyglotCLI 1.1.0" en lugar de solo "PolyglotCLI"**, a pesar de existir una columna propia para la versión. Se añade en `installer/PolyglotCLI.iss` una sección `[Registry]` que sobrescribe explícitamente `DisplayName={#MyAppName}` y `DisplayVersion={#APP_VERSION}` bajo `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1` (con `uninsdeletevalue`), garantizando que la concatenación "Nombre Versión" que aplican algunas vistas localizadas no se produzca.
- **La pantalla "Seleccione la carpeta de destino" informaba de solo 4,3 MB de espacio requerido, aunque la instalación completa ocupa ~510 MB** (169,7 MB del servidor + 340 MB del escritorio). Inno Setup solo contabiliza automáticamente en `ExtraDiskSpaceRequired` las directivas `[Files]` incondicionales; como las nuestras usan `Components:`, quedaban excluidas del cálculo. Se añade `ExtraDiskSpaceRequired={#EXTRA_SPACE_KB}` en `[Setup]`, donde el valor se calcula en `scripts/build_installer.ps1` recorriendo `artifacts/publish_out` y `artifacts/publish_maui` y se inyecta a ISCC vía `/DEXTRA_SPACE_KB=N`. Se incluye un valor por defecto conservador en el `.iss` para invocaciones directas de ISCC.
- **El instalador mostraba un checkbox "Abrir PolyglotCLI en el navegador al finalizar" en la última pantalla del asistente**. Se elimina completamente la sección `[Run]` de `installer/PolyglotCLI.iss` para que el asistente termine directamente en la pantalla de instalación finalizada, sin ofrecer lanzar el navegador ni el servidor web.
