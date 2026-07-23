# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [v1.1.1] - 2026-07-23

### Added

- Añadida comprobación previa e instalación automática desatendida de .NET 10 Desktop Runtime en el instalador Inno Setup (`.exe`).

### Fixed

- Corregida la compilación del instalador en CI/CD instalando Inno Setup en el runner de GitHub Actions y mejorando la localización dinámica de `ISCC.exe` vía `PATH` y carpetas estándar de Inno Setup 6/7.
- Corregida la desincronización de `UNRELEASE.md` y `CHANGELOG.md` al ejecutar la opción 6 de `run.ps1` (`scripts/bump_version.ps1`). Ahora el script detecta si el número de versión no cambia y revierte automáticamente las modificaciones locales en `csproj`, `CHANGELOG.md` y `UNRELEASE.md` si la operación se cancela o falla.

---

## [v1.1.0] - 2026-07-23

### Added

- Nuevo sistema de instalación basado en **Inno Setup 7** (`installer/PolyglotCLI.iss`, `installer/license.txt`, `scripts/build_installer.ps1`). Genera un instalador `.exe` con asistente gráfico nativo de Windows que reemplaza al antiguo `.msi` de WiX. Reutiliza los recursos del manifiesto (`assets/icons/app.ico`, `assets/msix/`) y la licencia MIT (`installer/license.txt`).
- Paquete NuGet **Cropper.Blazor 1.5.1** (`Cropper.Blazor`) referenciado desde `PolyglotCLI.web` y `PolyglotCLI.Maui`. Registra `builder.Services.AddCropper()` en ambos `Program.cs` / `MauiProgram.cs` para configurar el cliente JS de interop interno.
- **Historial de Trabajos (Web UI + Desktop MAUI)**: nuevo botón **Importar trabajo (.zpg)** en la esquina superior derecha de la página `/history`, junto al título, que abre un diálogo con selector de archivo `.zpg`/`.zip` y restaura la carpeta completa del trabajo en `%APPDATA%/PolyglotCLI/jobs/`. Si el `JobId` ya existe localmente el importador lo renombra automáticamente como `{JobId}_imported_{yyyyMMdd_HHmmss}` para evitar sobrescrituras.
- **Historial de Trabajos (Web UI + Desktop MAUI)**: nuevo botón **Exportar .zpg** (icono `archive`, color warning) por cada fila, que descarga la carpeta completa del trabajo como archivo `.zpg` (Zip Polyglot). Para trabajos en estado `InProgress` muestra un diálogo de confirmación explicando que el paquete será parcial e incluirá una nota `PACKAGE_NOTES.txt`.
- Servicio `PolyglotCLI.JobPackageService` (en `PolyglotCLI.core/Services/JobPackageService.cs`) con `ExportJobPackage(jobDir, outputStream)` e `ImportJobPackageAsync(inputStream, jobsRoot)`; maneja el renombrado en conflictos, valida que el ZIP contenga un `manifest.json` reconocible, limpia carpetas temporales de staging y registra la operación en `AppLogger`.
- Endpoints HTTP en el host web (`PolyglotCLI.web/Program.cs`): `GET /api/jobs/{jobId}/package` para descarga con `Content-Disposition: attachment`, y `POST /api/jobs/import` para subida multipart con `IFormFile` en el campo `file` (devuelve `{ jobId }` con el identificador efectivo).
- Abstracción multiplataforma `PolyglotCLI.IJobPackageHost` (`PolyglotCLI.core/Services/IJobPackageHost.cs`) con dos implementaciones: `PolyglotCLI.web.Services.WebJobPackageHost` (usa `NavigationManager` + `DialogService` + `HttpClient` contra los endpoints HTTP) y `PolyglotCLI.Maui.Services.MauiJobPackageHost` (usa `FilePicker.Default.PickAsync` para importar y `CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(...)` para mostrar el diálogo nativo "Guardar como" al exportar).

### Improved

- Workflow de release (`.github/workflows/release.yml`): el paso `Build MSI Installer (WiX)` se elimina. En su lugar se añade un paso `Build Setup EXE Installer (Inno Setup)` que invoca `pwsh scripts/build_installer.ps1 -Version <x.y.z> -NoPublish`. Los artefactos subidos a GitHub Release pasan de `PolyglotCLI-*.msi` a `installer/dist/PolyglotCLI-*-x64-setup.exe`.
- `scripts/install.ps1` (opción 7 de `run.ps1`): publica y compila el instalador Inno Setup, y luego lo ejecuta de forma interactiva. La desinstalación busca `%ProgramFiles%\FittyAr\PolyglotCLI\unins000.exe` (desinstalador nativo de Inno Setup) y, como respaldo, re-ejecuta el `.exe` con `/UNINSTALL`.
- `scripts/bump_version.ps1` (opción 6): la sección 5 ya no compila el proyecto WiX; en su lugar invoca `scripts/build_installer.ps1 -Version <x.y.z>` para validar la release construyendo el instalador Inno Setup end-to-end.
- **Verificador de Páginas (Web UI)**: la traducción se muestra ahora renderizada en Markdown mediante el componente `RadzenMarkdown`, con un botón que alterna entre modo Edición (TextArea con guardado automático) y Vista previa.
- **Verificador de Páginas (Web UI)**: los cambios manuales de traducción se rastrean en un diccionario `pendingTranslationEdits` por sesión, garantizando que el guardado del JSON aplique explícitamente todas las páginas editadas, no solo la última visitada. Se añade un icono `edit` naranja junto a cada página modificada en el listado izquierdo y un contador "X página(s) editada(s) en esta sesión" junto al encabezado de la página visualizada.
- **Verificador de Páginas (Web + Desktop MAUI)**: el visor de páginas renderizadas usa el componente `<CropperComponent>` de Cropper.Blazor con `DragMode = "move"` y crop-box deshabilitado, funcionando como visor de pan/zoom puro con gestos integrados (arrastrar para mover, rueda de ratón para zoom, doble clic para acercar).

### Changed

- **Reorganización profesional del árbol de carpetas del repositorio**. Las carpetas sueltas del root se consolidan en ubicaciones canónicas:
  - `prompts/` → `assets/prompts/` (prompts del sistema LLM).
  - `manifests/app.ico` → `assets/icons/app.ico` (icono oficial de la aplicación).
  - `manifests/msix/` → `assets/msix/` (manifiesto y assets del paquete MSIX).
  - `installer/build_installer/` → `installer/dist/` (salida compilada del instalador Inno Setup).
  - `publish_out/` y `publish_maui/` → `artifacts/publish_out/` y `artifacts/publish_maui/` (outputs de `dotnet publish`).
- **Migración a MAUI Blazor Hybrid**: Se migró la aplicación de escritorio a `PolyglotCLI.Maui` compartiendo los componentes Razor con `PolyglotCLI.web`.
- `installer/PolyglotCLI.iss`: actualizadas las rutas de los artefactos publicados (`..\artifacts\publish_out`, `..\artifacts\publish_maui`) y del icono (`..\assets\icons\app.ico`).
- **Selector de componentes en el instalador**: `full` (Instalación completa) y `custom` (Personalizada) para seleccionar `server` y `desktop`.
- **Personalización visual del instalador con `assets/`**: `WizardImageFile = assets/msix/Assets/LogoSimple.png` y `WizardSmallImageFile = assets/msix/Assets/Square44x44Logo.png`.
- **Accesos directos renombrados y agrupados**: Menú Inicio y Escritorio para Servidor Web, Navegador y Escritorio nativo.
- **Idioma del instalador**: sustituido a español (`compiler:Languages\Spanish.isl`).
- **Historial de Trabajos (Web + MAUI)**: el importador de `.zpg` mejora su diagnóstico de error en archivos corruptos o sin `manifest.json`.
- **Historial de Trabajos (MAUI)**: el botón Exportar `.zpg` ya no abre el archivo guardado con el diálogo "Abrir con…" del sistema; ahora muestra el diálogo nativo "Guardar como" mediante `CommunityToolkit.Maui.Storage.FileSaver`.
- **Empaquetador/importador de trabajos**: la extracción del ZIP se realiza directamente sobre la carpeta de staging, eliminando el bug de doble carpetizado.
- **Verificador de Páginas (Web UI)**: sustituido el desplegable de archivos por una lista visible de documentos y se amplió el panel derecho.

### Removed

- Proyecto `PolyglotCLI.Wix/` y carpeta `manifests/` del root eliminados por completo.
- Librería `panzoom` de timmywil (`wwwroot/lib/panzoom/`) y módulo JS de interoperabilidad (`wwwroot/js/panzoomInterop.js`) sustituidos por Cropper.Blazor.
- Referencias de configuración WiX en `bump_version.ps1`.

### Fixed

- **Error del instalador Inno Setup al no encontrar la carpeta `publish_out`** corregido actualizando rutas relativas a `artifacts/publish_out`.
- **El instalador .exe mostraba error de carpeta en equipos finales** corregido trasladando validaciones del `.iss` a `scripts/build_installer.ps1`.
- **Iconos genéricos en los accesos directos** solucionados asignando `<ApplicationIcon>` en los `.csproj` y `UninstallDisplayIcon` en el instalador.
- **Ventana vacía en el acceso directo "Escritorio nativo"** solucionada al embeber `app.ico` y corregir localización de `wwwroot/index.html`.
- **Nombre duplicado en "Programas y características"** corregido con sección `[Registry]` en Inno Setup.
- **Cálculo de espacio requerido en disco (510 MB)** inyectado a ISCC vía `/DEXTRA_SPACE_KB`.
- **Verificador de Páginas (Web + Desktop MAUI)**: solución de inconsistencias de pan/zoom y botones de control del visor con Cropper.Blazor.
- **Ampliado `MaximumReceiveMessageSize` de SignalR** a 32MB para transmitir imágenes base64 grandes.
- **Inicialización de componentes WebView2**: Solución de crash por configuración del modo del hilo de ejecución.
- **Referencia conflictiva de WindowsBase**: Solución de la advertencia MSB3277.

---

## [v1.0.1] - 2026-07-23

### Changed

- Modificado el ejecutable de la aplicación Blazor `PolyglotCLI.web` para iniciarse por defecto en **modo ventana nativo** (tipo MAUI/Desktop) ocultando la consola de comandos, y permitiendo opcionalmente correr en **modo servidor web clásico** pasándole el parámetro `--web`.
- Actualizado el instalador de WiX en `PolyglotCLI.Wix` para utilizar la interfaz estándar interactiva `WixUI_FeatureTree`. Ahora el usuario puede elegir si desea crear un acceso directo opcional en el Escritorio durante el proceso de instalación.
- Configurada la aplicación en `PolyglotCLI.Wix` y `AppxManifest.xml` para empaquetar y utilizar el icono oficial `app.ico` (generado a partir de la imagen corporativa `favicon.png`) en el ejecutable, los accesos directos de Inicio/Escritorio y el Panel de Control.
- Centralizado el icono de la aplicación en `app.ico`.
- Renombrados y migrados los activos de pantalla de bienvenida del manifiesto MSIX a la nomenclatura estándar de escala de Windows (`SplashScreen.scale-100.png` a `scale-400.png`) para el mapeo DPI dinámico automático en tiempo de ejecución.
- Reescrito el script de instalación `install.ps1` para compilar localmente y ejecutar de forma interactiva el instalador MSI nativo (`PolyglotCLI.Wix.msi`) mediante `msiexec`.
- Personalizados los fondos y banners gráficos de la interfaz de instalación interactiva de WiX utilizando la identidad corporativa.
- Integrado el acuerdo de licencia de usuario final (EULA) bajo los términos MIT oficiales de PolyglotCLI en formato RTF dentro del asistente de instalación interactiva de WiX.

---

## [v1.0.0] - 2026-07-23

### Added

- Creado el script de instalación dedicado `install.ps1` en `scripts/`.
- Creados los assets y el manifiesto MSIX en `manifests/msix/` para habilitar el empaquetado del instalador compatible con la Microsoft Store.
- Creado el proyecto de pruebas unitarias `PolyglotCLI.test` utilizando xUnit para validar la lógica de negocio principal de la aplicación (`TextChunker`, `DocumentExtractorFactory` y `AppConfig`).
- Métodos de sugerencia de modelos por defecto (`GetDefaultSuggestedModels` y `GetDefaultSuggestedVisionModels` en `LlmProvider.cs`) para usarlos como fallback.
- Descubrimiento y extracción 100% dinámica de modelos LLM vía endpoints API HTTP (`GET /v1/models`, `GET /api/tags`).
- Restricción estricta de selección en las pestañas OCR Process, Translation y Revision para permitir seleccionar únicamente los proveedores que han sido probados y verificados con éxito.
- Sistema de registro automático de servicios probados (`SaveTestedProvider`), que graba la URL, API Key y lista de modelos devueltos al pasar la prueba de conexión.
- Selección independiente de proveedor LLM por etapa (`OcrProvider`, `TranslationProvider`, `ReviewProvider`).
- Arquitectura multi-proveedor LLM modular con soporte nativo para Ollama, LM Studio, llama.cpp, OpenAI, Anthropic Claude, Google Gemini, Qwen, Kimi, MiniMax.
- Soporte para conversiones a DOCX, PDF y ODT de forma local (C#).
- Solución multiproyecto (`PolyglotCLI.core`, `PolyglotCLI.cli`, `PolyglotCLI.web`) para separar la lógica core compartida de las interfaces de presentación.

### Changed

- Modificado [run.ps1](file:///d:/GitHub/PolyglotCLI/run.ps1) para remover la instalación local e implementar una consola de desarrollo interactiva con opciones para iniciar la app, correr tests, compilar y ejecutar el pipeline de release.
- Actualizada la instrucción de la skill `changelog-helper` para registrar cambios en `docs/UNRELEASE.md`.
- Configurada la aceptación automática del EULA de WiX Toolset v7 en `PolyglotCLI.Wix.wixproj`.
- Modificado el workflow de GitHub Actions `release.yml` para compilar la aplicación, empaquetar el instalador `.msi` y el paquete `.msix`.
- Adaptadas las plantillas de GitHub Discussions y de reporte de problemas/propuestas de mejoras para referenciar a PolyglotCLI.

### Fixed

- Corregida la opción de salida (cambiada de `6` a `0`) en `run.ps1` utilizando la palabra clave `return`.
- Corregido el problema de visualización de caracteres especiales con acentos en la consola de PowerShell estableciendo la salida a UTF-8.
- Corregida la resolución de rutas en la previsualización de trabajos de la TUI.
- Corregida la selección del modelo en `ModelSelectionDialog` mediante la lectura directa de la propiedad nativa `listModels.SelectedItem`.
- Solucionada la interceptación del teclado en menús anidados introduciendo un contador dinámico de modales activos.
- Corregida la sincronización de `config.json` con el entorno de desarrollo local.
- Corregido error en el que las respuestas vacías devueltas por los modelos de traducción o revisión sobrescribían el contenido traducido original.
- Corregidos errores gráficos y de congelamiento/superposición de pantalla de la TUI en la terminal al salir.

### Removed

- Eliminado por completo el proyecto de interfaz de consola interactiva (`PolyglotCLI.cli`) para centralizar el desarrollo y las funcionalidades exclusivamente en la versión Web (`PolyglotCLI.web`).
