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
