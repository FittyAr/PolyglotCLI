# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.1.0] - 2026-07-23

### Added

### Improved

### Changed

- Reverted the `PolyglotCLI.web` project to a pure web server configuration.
- Migrated native desktop execution to the `PolyglotCLI.Maui` project using Blazor Hybrid.
- Moved `ApplicationMode` execution state to `PolyglotCLI.core` for clean sharing between projects.
- Conditionally hide the layout footer when running in desktop mode, keeping it visible only in web mode.
- Split the app execution command in run.ps1 into separate Desktop and Web options.
- Updated MSIX packaging to use the MAUI desktop application instead of the web server.
- Updated MSI installer to include both Server and Desktop applications as selectable features with independent shortcuts.

### Deprecated

### Removed

### Fixed

- Fixed window initialization crash of the WebView2 components by configuring the application thread mode.
- Resolved MSB3277 WindowsBase reference mismatch warning by enabling WPF framework references in the web project.

---

## [v1.0.1] - 2026-07-23

### Added

### Improved

### Changed
- Modificado el ejecutable de la aplicación Blazor `PolyglotCLI.web` para iniciarse por defecto en **modo ventana nativo** (tipo MAUI/Desktop) ocultando la consola de comandos, y permitiendo opcionalmente correr en **modo servidor web clásico** pasándole el parámetro `--web`.
- Actualizado el instalador de WiX en `PolyglotCLI.Wix` para utilizar la interfaz estándar interactiva `WixUI_FeatureTree`. Ahora el usuario puede elegir si desea crear un acceso directo opcional en el Escritorio durante el proceso de instalación.
- Configurada la aplicación en `PolyglotCLI.Wix` y `AppxManifest.xml` para empaquetar y utilizar el icono oficial `app.ico` (generado a partir de la imagen corporativa `favicon.png`) en el ejecutable, los accesos directos de Inicio/Escritorio y el Panel de Control.
- Centralizado el icono de la aplicación en [app.ico](file:///d:/GitHub/PolyglotCLI/manifests/app.ico), eliminando las copias redundantes y configurando MSBuild y WiX para referenciar la fuente de verdad única en tiempo de compilación.
- Renombrados y migrados los activos de pantalla de bienvenida del manifiesto MSIX a la nomenclatura estándar de escala de Windows (`SplashScreen.scale-100.png` a `scale-400.png`) para el mapeo DPI dinámico automático en tiempo de ejecución.
- Reescrito el script de instalación [install.ps1](file:///d:/GitHub/PolyglotCLI/scripts/install.ps1) para compilar localmente y ejecutar de forma interactiva el instalador MSI nativo (`PolyglotCLI.Wix.msi`) mediante `msiexec`, eliminando las descargas de archivos ZIP y permitiendo probar localmente el flujo de instalación de WiX idéntico a como se haría desde GitHub Actions.
- Personalizados los fondos y banners gráficos de la interfaz de instalación interactiva de WiX (`installer_dialog.png` y `installer_banner.png` en `manifests/`) utilizando la identidad corporativa y adaptando los márgenes y contrastes para garantizar la perfecta legibilidad de los textos nativos del asistente.
- Integrado el acuerdo de licencia de usuario final (EULA) bajo los términos MIT oficiales de PolyglotCLI en formato RTF ([license.rtf](file:///d:/GitHub/PolyglotCLI/manifests/license.rtf)) dentro del asistente de instalación interactiva de WiX.

### Deprecated

### Removed

### Fixed

---

## [v1.0.0] - 2026-07-23

### Added

### Improved

### Changed

### Deprecated

### Removed

### Fixed

---

## [v1.0.0] - 2026-07-23

### Added

### Improved

### Changed
- Reubicado el script de instalación dedicada [install.ps1](file:///d:/GitHub/PolyglotCLI/scripts/install.ps1) a la carpeta `scripts/`, adaptando la resolución de rutas para que se ejecute relativo a la raíz del repositorio de forma correcta.
- Modificado [run.ps1](file:///d:/GitHub/PolyglotCLI/run.ps1) en la raíz para incluir la opción `6` en el menú interactivo, la cual invoca directamente al instalador local reubicado.

### Deprecated

### Removed

### Fixed

---

## [v1.0.0] - 2026-07-23

### Added
- Creado el script de instalación dedicado [install.ps1](file:///d:/GitHub/PolyglotCLI/install.ps1) reubicando la lógica de instalación y desinstalación local.
- Creados los assets y el manifiesto MSIX en la ruta `manifests/msix/` para habilitar el empaquetado del instalador compatible con la Microsoft Store.

### Changed
- Modificado [run.ps1](file:///d:/GitHub/PolyglotCLI/run.ps1) para remover la instalación local e implementar una consola de desarrollo interactiva con opciones para iniciar la app, correr tests, compilar y ejecutar el pipeline de release (commit, tag, push en GitHub y migración automática de novedades de `docs/UNRELEASE.md` a `docs/CHANGELOG.md`).
- Actualizada la instrucción de la skill [changelog-helper](file:///d:/GitHub/PolyglotCLI/.agents/skills/changelog-helper/SKILL.md) para indicar a los agentes de IA que deben registrar sus cambios de desarrollo en `docs/UNRELEASE.md` en lugar de `CHANGELOG.md` en la raíz.
- Configurada la aceptación automática del EULA de WiX Toolset v7 en [PolyglotCLI.Wix.wixproj](file:///d:/GitHub/PolyglotCLI/PolyglotCLI.Wix/PolyglotCLI.Wix.wixproj) para evitar errores de compilación asociados al OSMF (Open Source Maintenance Fee) en compilaciones locales y de CI/CD.
- Modificado el workflow de GitHub Actions [release.yml](file:///d:/GitHub/PolyglotCLI/.github/workflows/release.yml) para compilar la aplicación, empaquetar el instalador `.msi` (mediante WiX v7) y el paquete `.msix` (para la Windows Store), subiendo exclusivamente ambos formatos en las publicaciones de GitHub Releases y removiendo el archivo comprimido `.zip`.
- Adaptadas las plantillas de GitHub Discussions y de reporte de problemas/propuestas de mejoras en [10_bug_report.yml](file:///d:/GitHub/PolyglotCLI/.github/ISSUE_TEMPLATE/10_bug_report.yml), [20_feature_request.yml](file:///d:/GitHub/PolyglotCLI/.github/ISSUE_TEMPLATE/20_feature_request.yml) y [config.yml](file:///d:/GitHub/PolyglotCLI/.github/ISSUE_TEMPLATE/config.yml) para referenciar a PolyglotCLI, sus archivos de configuración (`config.json`), archivos de bitácora (`polyglot.log`) y su respectivo foro de discusiones en el repositorio de GitHub.

### Fixed
- Corregida la opción de salida (cambiada de `6` a `0`) en [run.ps1](file:///d:/GitHub/PolyglotCLI/run.ps1) utilizando la palabra clave `return` para salir correctamente del bucle infinito de la consola interactiva.
- Corregido el problema de visualización de caracteres especiales con acentos en la consola de PowerShell en [run.ps1](file:///d:/GitHub/PolyglotCLI/run.ps1), [install.ps1](file:///d:/GitHub/PolyglotCLI/install.ps1) y [bump_version.ps1](file:///d:/GitHub/PolyglotCLI/scripts/bump_version.ps1) estableciendo la salida a UTF-8 y guardando los archivos con codificación UTF-8 con BOM.

### Removed
- Eliminado por completo el proyecto de interfaz de consola interactiva (`PolyglotCLI.cli`) para centralizar el desarrollo y las funcionalidades exclusivamente en la versión Web (`PolyglotCLI.web`).

### Added
- Creado el proyecto de pruebas unitarias `PolyglotCLI.test` utilizando xUnit para validar la lógica de negocio principal de la aplicación (`TextChunker`, `DocumentExtractorFactory` y `AppConfig`).

### Improved
- **Optimización de Interfaz en Detalles del Trabajo**:
  - Reducido el ancho del panel lateral de "Páginas Renderizadas (PNG)" en la pestaña de imágenes extraídas para maximizar el espacio del visor.
  - Configurado el visor de imágenes extraídas para escalar las páginas al 100% de su ancho disponible, permitiendo scroll vertical y mejorando sustancialmente la legibilidad de planos o diagramas.
  - Ajustada la altura del visor de archivos de texto y la consola de logs en la pestaña de bitácoras para aprovechar al máximo el espacio vertical del cuadro contenedor.

### Fixed
- **Inicialización del Instalador**: Corregida la definición de `$scriptDir` en `install.ps1` para asegurar la copia correcta del directorio de prompts durante la instalación.
- **Robustez en la Obtención de Releases**: Corregido el comportamiento del instalador en caso de fallar la conexión con la API de GitHub (como en repositorios privados o sin releases públicos), permitiendo compilar e instalar desde fuentes locales de forma interactiva si se detecta el SDK de .NET.
- **Copia de Formatos de Conversión**: Corregido el flujo del orquestador y la exportación manual para copiar todos los formatos generados (DOCX, PDF, HTML, ODT, etc.) al subdirectorio `outputs` de cada trabajo, asegurando que se visualicen correctamente en la pestaña de archivos generados.
- **Verificador de Páginas en Pestañas**: Se ha rediseñado la interfaz del verificador de páginas dividiendo ambos paneles en pestañas. El panel izquierdo ahora permite comparar la imagen original de la página con el texto plano extraído (OCR) completamente limpio (sin trazas de razonamiento), mientras que el panel derecho separa la edición de la traducción de las trazas de pensamiento.
- **Visor de Trazas de Pensamiento (Reasoning)**: Soporte para procesar, extraer y persistir los bloques `<think>...</think>` que envían los modelos de razonamiento (tanto en la fase de extracción OCR como en la de traducción) en el JSON del trabajo. Los usuarios pueden visualizar cómodamente qué pensó el modelo en cada página de forma opcional a través de una pestaña dedicada en el verificador de páginas.
- **Script de Migración de Trazas**: Se desarrolló y ejecutó un script automatizado para migrar todas las trazas de razonamiento existentes de los trabajos anteriores (por ejemplo, el trabajo `20260722_143935`), extrayendo el `<think>` tanto de las traducciones como de los textos OCR originales al campo `ThoughtText` para mantener la consistencia.
- **Re-procesamiento de Páginas Individuales**: Posibilidad de volver a procesar y traducir una única página que haya fallado o requiera mejoras, de forma aislada y sin necesidad de reiniciar todo el trabajo desde el principio.
- **Control de Módulos Activos**: Nueva sección en la configuración para habilitar o deshabilitar de forma independiente cada una de las fases del proceso: Extracción de texto/OCR, Traducción con Inteligencia Artificial, Revisión de traducción y Conversión final a formatos (como Word o PDF). Las fases desactivadas serán omitidas automáticamente para ahorrar tiempo y recursos.
- **Diseño a Pantalla Completa**: Rediseño responsivo de la ventana de detalles de trabajos para aprovechar más del 90% del tamaño de tu pantalla. Los textos, consolas y visor de páginas del PDF se adaptan dinámicamente y se muestran a gran escala para una lectura cómoda.
- **Consola de Progreso Avanzada**: Añadidos controles interactivos que permiten maximizar o restaurar la consola de comandos de fondo, así como activar o desactivar el desplazamiento automático (autoscroll) de los logs en pantalla.
- **Cancelación en Caliente**: Añadido un botón para detener de forma inmediata y segura cualquier trabajo de traducción que se encuentre en ejecución activa.
- **Exportación y Conversión Manual**: Incorporación de un botón en el historial de trabajos para regenerar y forzar la exportación de tus traducciones a Markdown y formatos de procesador de textos (DOCX, PDF) en cualquier momento.
- **Eliminación Segura**: Capacidad para borrar físicamente del disco la carpeta de datos de cualquier trabajo seleccionado desde la interfaz web, tras una confirmación de seguridad.

### Added
- Métodos de sugerencia de modelos por defecto (`GetDefaultSuggestedModels` y `GetDefaultSuggestedVisionModels` en `LlmProvider.cs`) para usarlos como fallback en la interfaz de usuario en las pestañas de configuración cuando el proveedor no tiene modelos probados o almacenados.
- Eliminadas totalmente las listas de modelos hardcodeadas en favor de descubrimiento y extracción 100% dinámica vía endpoints API HTTP (`GET /v1/models`, `GET /api/tags`), con soporte universal para esquemas JSON con propiedades `data`, `models` o listas de cadenas.
- Restricción estricta de selección en las pestañas OCR Process, Translation y Revision para permitir seleccionar únicamente los proveedores que han sido probados y verificados con éxito en la pestaña General (vía `AppConfig.GetTestedProviders`).
- Sistema de registro automático de servicios probados (`SaveTestedProvider`), que graba la URL, API Key y lista de modelos devueltos al pasar la prueba de conexión ("PROBAR CONEXIÓN").
- Selección independiente de proveedor LLM por etapa (`OcrProvider`, `TranslationProvider`, `ReviewProvider`) permitiendo combinar distintos proveedores (ej. Ollama local en OCR, MiniMax en Traducción y Gemini en Revisión).
- Actualización dinámica del desplegable de modelos en las pestañas de OCR Process, Translation y Revision según el proveedor seleccionado para esa etapa específica.
- Arquitectura multi-proveedor LLM modular con soporte nativo para **Ollama**, **LM Studio**, **llama.cpp**, **OpenAI / OpenCode**, **Anthropic Claude**, **Google Gemini**, **Qwen / DashScope**, **Kimi / Moonshot**, **MiniMax** y endpoints personalizados OpenAI-compatible.
- Interfaz genérica `ILlmClient` y fábrica `LlmClientFactory` para instanciar clientes HTTP desacoplados del resto de la aplicación.
- Clientes nativos `OpenAiCompatibleClient`, `AnthropicClient` (API `/v1/messages`) y `GeminiClient` (API REST `/v1beta/models/...`).
- Soporte para claves API independientes por proveedor (`ProviderApiKeys` / `GetApiKeyForProvider`) permitiendo almacenar credenciales individuales para cada servicio sin reescribirlas al alternar proveedores.
- Nuevos parámetros CLI `--provider` / `-p` y `--api-key` / `-key` para configurar el servicio LLM dinámicamente.
- Selector de proveedor LLM y campo de API Key en los paneles de configuración TUI (`SettingsDialog`) y Web Blazor (`GeneralConfigTab.razor`).
- Agregado el control "Idioma Destino por Defecto" (`TargetLanguage`) en la pestaña de traducción del panel de configuración de la Web, garantizando la paridad total con las propiedades persistidas de `AppConfig`.
- Añadidos controles interactivos en el Dashboard Web (`Home.razor`) para habilitar/deshabilitar de forma selectiva las fases de extracción/OCR (`Transcribe`), traducción (`Translate`) y el modo prueba (`Debug`), igualando las capacidades del CLI.
- Implementado el soporte para visualizar y editar la lista de extensiones de archivos compatibles (`SupportedInputExtensions`) en la sección de formatos del panel de configuración web.
- Interfaz de configuración completa tabulada (`Config.razor`) con pestañas para General & API, Modelos LLM, OCR & Fragmentación, Formatos/Reglas y Prompts del Sistema, vinculando el 100% de las opciones en `AppConfig`.
- Integrada la carga dinámica de modelos LLM desde la API local de LM Studio en todos los selectores de modelos del formulario de configuración.
- Migración al tema nativo de Radzen `material-dark.css` en la UI Web para armonizar el contraste visual de textos, inputs y botones en modo oscuro.
- Rediseño de la hoja de estilos (`app.css`) para corregir contrastes de color, añadir efectos de glassmorphism y mejorar la legibilidad general.
- Nueva interfaz de usuario Web moderna para el dashboard principal, implementando Dark Mode, tipografía *Inter* y componentes avanzados de Blazor (Glassmorphism).
- Consola virtual embebida en el frontend Web para monitorear el progreso de la traducción (y OCR) en tiempo real mediante *streaming asíncrono*.
- Pestaña "Historial de Trabajos" (`History.razor`) en la UI Web que despliega un listado de trabajos en una tabla de datos (`RadzenDataGrid`), leyendo los metadatos dinámicamente de `/jobs/`.
- Integración en la vista web de la funcionalidad **Análisis de Errores IA** y la opción de retomar ejecuciones, igualando en funcionalidad a la interfaz CLI.
- Sistema de eventos en `AppLogger` (`OnLogMessage`) para difundir de manera no-bloqueante registros directamente hacia clientes Blazor Server.
- Pestaña "Historial de Trabajos" en la interfaz de usuario (TUI) para visualizar el estado detallado de trabajos anteriores y reanudarlos manualmente.
- Sistema de manifiesto (`JobManifest` / `manifest.json`) que guarda de forma incremental y persistente el progreso de OCR, traducción y revisión de cada página/chunk solo cuando finaliza exitosamente.
- Soporte en el orquestador (`TranslationOrchestrator`) para reanudar trabajos a partir de su manifiesto, recuperando archivos parciales de la carpeta del trabajo y omitiendo páginas procesadas con éxito.
- Opción `--resume-job <id>` en la interfaz de línea de comandos para reanudar trabajos directamente.
- Soporte nativo para conversiones a DOCX, PDF y ODT de forma local (C#).
- Configuraciones dinámicas de extensiones de entrada y formatos de salida en config.json.
- Visor de trabajos (`JobViewerDialog`) que muestra los metadatos JSON y estados de error mediante un árbol de directorios directamente en la TUI, e incluye la exportación manual a Markdown (`[V]`).
- Prompt interactivo de Análisis de Errores al fallar un trabajo en consola, el cual recopila errores OCR y de traducción y sugiere modificaciones empleando el asistente LLM (Prompt Helper).
- Agregado el proyecto `PolyglotCLI.web` implementando una interfaz moderna con Blazor Server y Radzen Blazor Components para la configuración y ejecución de trabajos.
- Métodos `SavePresets(...)` y `UpdateAndSaveSettings(...)` en `AppConfig` para centralizar la configuración rápida y avanzada.
- Método `GetAvailableModelsAsync()` en `LmStudioClient` y `TestApiConnectionAsync(...)` en `ModelManagerService` para encapsular la interacción HTTP y comprobación de la API de LM Studio.
- Métodos `LoadPastJobs()`, `GetJobDataFiles(...)` y `GetJobDataPages(...)` en `JobManifestService` para delegar la administración del historial de trabajos.
- Nuevo servicio `DocumentDiscoveryService` para abstraer el escaneo de directorios.
- Nuevo servicio `JobValidator` para centralizar las reglas de validación de trabajos, requisitos de modelo de visión y validaciones de acciones de mejora/análisis de prompt.
- Nuevo servicio `JobExportService` para encapsular la exportación a Markdown de los resultados de un trabajo.
- Interfaz de configuración completa tabulada (`Config.razor`) con pestañas para General & API, Modelos LLM, OCR & Fragmentación, Formatos/Reglas y Prompts del Sistema, vinculando el 100% de las opciones en `AppConfig`.
- Creada la solución multiproyecto para separar la lógica core compartida (`PolyglotCLI.core`) de las interfaces de CLI y Web.
 
### Changed
- Unificada la resolución de rutas y el acceso al sistema de archivos de prompts en el Core y la Web migrando toda la lógica directa hacia `PromptLoader.cs`.
- Refactorizada la clase `Config.razor.cs` en la UI Web para cargar y guardar los prompts utilizando exclusivamente los métodos centralizados de `PromptLoader`.
- Refactorizada la clase `PromptHelperService.cs` en el Core para utilizar el servicio `PromptLoader` al optimizar prompts en lugar de usar rutas de archivos hardcodeadas.
- Reemplazada la consulta manual HTTP a la API de LM Studio en la interfaz Web (`Config.razor.cs`) por llamadas unificadas a `LmStudioClient.GetAvailableModelsAsync()` del Core, reduciendo redundancia.
- Rediseñado el flujo de reanudación de trabajos en la interfaz Web (`History.razor` -> `Home.razor`) usando redirecciones con el query parameter `resumeJobId` para visualizar de forma interactiva y en tiempo real el progreso en la consola principal del Home.
- Integrada la validación de rango de páginas de archivos PDF en la vista principal del Traductor Web mediante la función del Core `CommandLineOptions.IsValidPageRange(...)`.
- Simplificada la persistencia de preajustes en `Home.razor.cs` delegando toda la lógica de guardado y cálculo de formatos al método `Config.SavePresets(...)` de Core.
- Integradas las validaciones robustas y centralizadas de `JobValidator` del Core en la interfaz Web para verificar las configuraciones de traducción, los requerimientos de modelo de visión (OCR), el análisis de archivos y la optimización de prompts antes de ejecutar las llamadas al LLM.
- Unificada la obtención del historial de trabajos pasados y la generación del resumen de errores en `History.razor` delegando estas tareas a `JobManifestService.LoadPastJobs()` y `JobManifestService.BuildErrorSummary(job)` en el Core.
- Desacoplado el acceso a la API, la validación de configuraciones, el escaneo de directorios y la persistencia/exportación de archivos del proyecto CLI (`PolyglotCLI.cli`) hacia el proyecto Core (`PolyglotCLI.core`), refactorizando `ConsoleErrorAnalysisService`, `ModelSelectionDialog`, `SettingsDialog`, `InteractiveMenu.Actions`, `InteractiveMenu.Dialogs` y `JobViewerDialog`.
- Corregida la resolución de rutas en la previsualización de trabajos de la TUI (`ViewSelectedJob`) para utilizar consistentemente `TranslationOrchestrator.GetJobsDirectory()`, eliminando directorios de jobs relativos y locales.
- Refactorización de la pestaña principal del traductor (`Home.razor`) para adhieres al Principio de Responsabilidad Única (SRP), separando la interfaz de usuario de su lógica de control (`Home.razor.cs`), extrayendo los tipos `FileSystemItem` y `LogEntry` a sus propios archivos, y desacoplando los diálogos Radzen de previsualización y progreso a componentes independientes (`ImprovedPromptDialog`, `AnalyzeFilePromptDialog`, `ProgressDialog`) y un modelo observable `ProgressState` de actualización reactiva.
- Añadido soporte de recarga en caliente (`AppConfig.Reload()`) para recargar dinámicamente `config.json` desde el disco al inicializar el Traductor y la Configuración Web, garantizando la consistencia y sincronía de los ajustes con cambios realizados externamente (por el CLI o TUI).
- Habilitada la interactividad global de Blazor Server en `App.razor` agregando el atributo `@rendermode="InteractiveServer"` a `HeadOutlet` y `Routes`, solucionando los problemas de botones e inputs que no respondían.
- Rediseñada la pestaña "Traductor" (`Home.razor`) reemplazando el cuadro de texto de archivos de entrada por un explorador de carpetas interactivo con selector de unidades de disco, navegación mediante clicks sobre carpetas/`..` y un grid editable para configurar de forma individual el método y rango de páginas por archivo sin conflictos de foco.
- Incorporado el panel de Prompt Adicional en la pestaña "Traductor" con botones de acción para Analizar Archivo (IA), Mejorar Prompt (IA) con previsualización lado a lado y Guardar Preajustes en `config.json`.
- Rediseñada por completo la pestaña de "Configuración" (`Config.razor`) para alinearse con los menús de la TUI/CLI (General, OCR Process, Translation, Revision, Output Formats, Prompts del Sistema), incluyendo el botón para probar conexión de LM Studio, la población dinámica de dropdowns y la adición de botones de actualización de modelos junto a cada combobox; posteriormente refactorizada aplicando el principio de responsabilidad única (SRP), separando su lógica en un archivo code-behind (`Config.razor.cs`) y dividiendo cada sección/pestaña de configuración en sub-componentes independientes (`GeneralConfigTab`, `OcrConfigTab`, `TranslationConfigTab`, `RevisionConfigTab`, `OutputConfigTab`, `PromptsConfigTab`).
- Incorporada la edición interactiva de los archivos de prompts físicos del sistema (`ocr_prompt.md`, `translation_prompt.md`, `review_prompt.md` y `prompt_improver_prompt.md`) en la pestaña de 'Prompts del Sistema' de `Config.razor` mediante acordeones colapsables (`RadzenAccordion`).
- Reubicado el campo técnico de Timeout de Mejora de Prompt a la pestaña General de la configuración.
- Reemplazada la entrada libre de formato de salida por un control `RadzenDropDown` enlazado dinámicamente a los formatos admitidos desde la configuración (`Config.SupportedOutputFormats`).
- Simplificada la interfaz de la pestaña "Traductor" removiendo los checkboxes redundantes de configuración global y el selector de modelo de traducción que ya se editan en la pestaña "Configuración", y eliminando el campo redundante de idioma de destino de la configuración global ya que se define en cada trabajo.
- Ignorado el directorio de instalación local de .NET SDK (`local-dotnet/`) en `.gitignore`.
- Reestructurada la interfaz principal de la TUI incorporando navegación lateral por pestañas ("Traductor" e "Historial de Trabajos").
- Modificado `CommandLineOptions` para aceptar el identificador de reanudación y omitir la validación de archivos de entrada cuando se retoma un trabajo.
- Eliminada la generación y escritura de archivos intermedios `.md` (live-writing) en tiempo real, migrando la persistencia de los extractores a retornos asíncronos y almacenamiento exclusivo en los archivos de estado `manifest.json` mediante metadatos.
- Implementada lógica de reintento dinámico no recursivo para fallos de traducción no relacionados a red, escalando gradualmente la temperatura del LLM desde su valor inicial hasta un límite seguro máximo de `0.6`.
- Refactorizado el orquestador monolítico `TranslationOrchestrator.cs` aplicando el Principio de Responsabilidad Única (SRP), extrayendo la persistencia de manifiestos a `JobManifestService.cs`, la gestión de modelos y memoria a `ModelManagerService.cs` y la interactividad de consola a `ConsoleErrorAnalysisService.cs`.
- Desacoplados los servicios principales (`TranslationOrchestrator`, `TranslatorService`, `OcrService`, `ReviewService`) de llamadas de consola bloqueantes y directas (como `Console.WriteLine` o `Console.ForegroundColor`), promoviendo el uso de eventos de `AppLogger` para asegurar la compatibilidad con entornos Web y de Interfaz Gráfica (Blazor).
- Reestructurada la arquitectura de la aplicación en tres proyectos independientes (`PolyglotCLI.core`, `PolyglotCLI.cli`, `PolyglotCLI.web`) bajo una única solución `PolyglotCLI.slnx` para maximizar la reutilización del código.

- Corregida la captura de teclas de configuración (barra espaciadora, T, M, P) y la tecla Enter en la TUI al usar la abstracción Key de Terminal.Gui v2 en lugar de comparaciones directas con KeyCode.
- Habilitada la navegación de foco con la tecla Tab en la TUI estableciendo CanFocus = true en los contenedores genéricos principales (_tabContainer, _translatorView, _jobsHistoryView), permitiendo el acceso e interacción con el listado de archivos y demás controles.
- Reorganizado el directorio de cada trabajo (`jobs/<id>`) para clasificar sus archivos en subcarpetas descriptivas (`sources/`, `temp/`, `logs/`, `data/`, `outputs/`), reduciendo el desorden visual y manteniendo la compatibilidad de reanudación y lectura en el historial para trabajos creados con la estructura antigua.
- Corregido el foco inicial en los diálogos modales (como el modal de rango de páginas y el selector de modelos); se cambiaron los botones a adición normal en coordenadas explícitas en lugar de usar `dialog.AddButton()`, logrando que el foco caiga de inmediato sobre el cuadro de texto (`textInput`) o el listado de modelos (`listModels`) y permitiendo la escritura y navegación instantáneas sin necesidad de presionar la tecla `Tab` para enfocar los controles.
- Corregida la selección del modelo en `ModelSelectionDialog` mediante la lectura directa de la propiedad nativa `listModels.SelectedItem` al confirmar el diálogo, eliminando variables de seguimiento inestables y asegurando la correcta actualización tanto de la interfaz de usuario como del archivo de configuración.
- Solucionada la interceptación del teclado en menús anidados introduciendo un contador dinámico de modales activos en `InteractiveMenu` (`OpenModal()` / `CloseModal()`), previniendo que la salida de un diálogo secundario (como el selector de modelos) reactivara incorrectamente el teclado de fondo del menú principal mientras el diálogo de Ajustes permanecía abierto.
- Corregida la sincronización de `config.json` con el entorno de desarrollo local; ahora la aplicación prioriza el archivo `config.json` del directorio de trabajo actual si existe, y escribe los cambios de configuración de vuelta en el mismo archivo cargado en lugar de forzarlos siempre en `AppData`. Esto resuelve el problema por el cual los cambios (como la selección del modelo) no se veían reflejados en el repositorio local ni en la solución.
- Mejorada la usabilidad del modal "Page Range", permitiendo que el diálogo se acepte y guarde de forma automática al presionar directamente la tecla `Enter` desde la caja de texto.
- Corregido error en el que las respuestas vacías devueltas por los modelos de traducción o revisión sobrescribían el contenido traducido original.
- Corregidos errores gráficos y de congelamiento/superposición de pantalla de la TUI en la terminal al salir, inicializando y disponiendo de forma limpia la instancia `IApplication` de `Terminal.Gui` v2.
- Desactivada la inserción de tabuladores en cuadros de texto multilinea (`SafeTextView`), habilitando la navegación nativa de foco con la tecla `Tab`.
- Corregida la posición flotante desplazada de los popups de `DropDownList` (formatos de salida) asignándoles una altura inicial `Height = 1`.
- Aumentado el contraste y la definición visual de modales y paneles mediante la aplicación de bordes redondeados (`LineStyle.Rounded`) en todas las vistas de tipo `Dialog` y `FrameView`.
- Corregido el solapamiento visual ("ghosting") entre paneles del diálogo de configuración avanzada al cambiar de categoría, forzando un redibujado explícito (`SetNeedsDraw`) en cada cambio de visibilidad.
- Corregido el comportamiento de "Save Presets" que no persistía el estado del checkbox "Gen Doc", el formato seleccionado, ni el estado de revisión; ahora guarda `DefaultOutputFormat`, `OutputFormats` y `EnableReview` en `config.json`.
- Eliminadas 57 advertencias de compilación (`CS8618`, `CS8604`, `CS8602`, `CS8622`) en los archivos de la TUI mediante declaración de campos UI como nullable, uso del accessor `AppRequired` en lugar de `_app` directamente, y firmas correctas en lambdas de `KeyDown`.
- Corregida la regresión por la que las teclas `T`/`M` (alternar modo OCR) y `P` (rango de páginas) dejaban de funcionar sobre el listado de archivos; movidas al handler global `AppRequired.Keyboard.KeyDown` con guarda `HasFocus`, ya que `Terminal.Gui.Editor.Editor` intercepta teclas de carácter a nivel de aplicación antes de que lleguen al `ListView`.
- Solucionado el problema en la ventana de ajustes donde la selección de un modelo LM Studio desde el servidor no se reflejaba visualmente de inmediato, aplicando llamadas explícitas a `SetNeedsDraw()` en el campo de texto actualizado.
- Solucionado el problema del modal de ayuda (F1), el cual ahora tiene un alto reducido a 18 y utiliza un visor de texto interactivo (`SafeTextView`) con scroll vertical y envoltura de palabras para evitar el desborde y permitir leer toda la información de atajos.

### Changed
- Migración de `SafeTextView` desde el obsoleto `Terminal.Gui.Views.TextView` (CS0618) al nuevo `Terminal.Gui.Editor.Editor` del paquete `Terminal.Gui.Editor` v2.5.7; esta es la migración recomendada por la propia librería.
- Corrección del crash en el constructor de `SafeTextView` al inicializar `IndentationSize = 0` (valor mínimo requerido es 1 por la API de `Terminal.Gui.Editor`).

- Integración de las librerías open-source HtmlToOpenXml, PeachPDF y NetOdt como fallback local cuando pandoc no está disponible.
- Actualización de los menús interactivos y de ajustes para consumir dinámicamente los formatos de salida soportados.
- Adaptación de la configuración de agentes (`AGENTS.md`) de Rust/Pairee a C#/.NET 10/PolyglotCLI.
- Adaptación de las herramientas de asistencia (skills) `settings-helper` y `changelog-helper` al contexto del proyecto.
- Reemplazo de la habilidad `localize-helper` por `prompts-helper` adaptada al sistema de prompts del proyecto.
- Migración del almacenamiento de configuraciones, temporales, logs y archivos resultantes al directorio `%appdata%\PolyglotCLI` en sistemas Windows.
- Actualización completa de la interfaz gráfica de terminal (TUI) para adaptarla a los cambios de la versión 2.4.17 de `Terminal.Gui`.
- Modularización completa del archivo monolítico `InteractiveMenu.cs` mediante una clase parcial no estática, organizando el diseño, eventos, acciones y diálogos asistentes en archivos independientes bajo el directorio `Services/InteractiveMenu/`.
