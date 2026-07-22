# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Añadidos botones interactivos para maximizar/restaurar la consola de progreso y para activar/desactivar el desplazamiento automático (autoscroll) dinámico.
- Añadido un botón "Detener Proceso" en la interfaz de usuario web que permite cancelar de forma inmediata y segura una ejecución de OCR/traducción activa.
- Añadido un botón de eliminación en el historial de trabajos que permite borrar físicamente del disco la carpeta de datos de cualquier trabajo seleccionado (`jobs/<jobId>`) tras confirmación interactiva.

### Changed
- Integrada la biblioteca `Microsoft.SemanticKernel` (versión `1.78.0`) en el proyecto central, reemplazando la serialización JSON manual y las llamadas directas HTTP de `OpenAiCompatibleClient` y `GeminiClient` por conectores nativos oficiales (`OpenAI` y `Google` experimental `1.78.0-alpha`).
- Simplificado `PromptHelperService` (métodos `ImprovePromptAsync`, `GenerateContextPromptAsync` y `AnalyzeErrorsAsync`) para reutilizar `LlmClientFactory` y la interfaz `ILlmClient` eliminando el código HTTP e inyecciones HttpClient repetitivas.
- Eliminación completa del proveedor Anthropic Claude y su cliente dedicado `AnthropicClient` para simplificar la arquitectura, dejando únicamente los conectores oficiales integrados de Semantic Kernel.

### Fixed
- Corregido el dominio del endpoint por defecto de MiniMax a `api.minimax.io` en lugar del dominio obsoleto `.chat`.
- Implementada la interrupción inmediata de todo el trabajo ante errores críticos de autenticación o API Key inválida (HTTP 401 Unauthorized), impidiendo ejecuciones fallidas repetidas en cascada por cada página del documento.
- Implementada la consulta automática a internet (asíncrona y en segundo plano) de los modelos disponibles al cambiar el proveedor en el combobox de la UI web.
- Restringida la limpieza y validación de VRAM únicamente a los proveedores locales y compatibles (Ollama, LM Studio), eliminando advertencias confusas e incorrectas sobre servidores en la nube como MiniMax, Gemini o OpenAI.
- Corregida la actualización inmediata del selector de modelos en la interfaz web de configuración al cambiar de proveedor en las pestañas de OCR, traducción y revisión, limpiando o preseleccionando modelos válidos del nuevo proveedor.
- Añadida precarga de modelos sugeridos por defecto para proveedores no conectados en la configuración de la Web, y actualizados los modelos de MiniMax con la serie oficial actual (MiniMax-M3, M2.7, M2.5, etc.) en lugar de la serie obsoleta abab6.5.
- Corregida la sincronización de credenciales y URL en las pruebas de conexión del panel de configuración de la Web, asegurando que los valores editados en tiempo real en los textboxes tengan prioridad inmediata al realizar pruebas y listar modelos del proveedor seleccionado.
- Corregida la resolución de la carpeta de prompts en `PromptLoader` para ascender por los directorios padres buscando la carpeta `prompts` real del proyecto raíz, eliminando la creación de archivos vacíos accidentales en `PolyglotCLI.web/prompts`.
- Actualizada la dependencia del paquete `AngleSharp` a la versión `1.5.2` en `PolyglotCLI.core.csproj` para resolver programáticamente la vulnerabilidad NU1902 (GHSA-pgww-w46g-26qg) sin anular ni ignorar advertencias del compilador.

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
