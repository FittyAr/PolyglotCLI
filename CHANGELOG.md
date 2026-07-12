# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Pestaña "Historial de Trabajos" en la interfaz de usuario (TUI) para visualizar el estado detallado de trabajos anteriores y reanudarlos manualmente.
- Sistema de manifiesto (`JobManifest` / `manifest.json`) que guarda de forma incremental y persistente el progreso de OCR, traducción y revisión de cada página/chunk solo cuando finaliza exitosamente.
- Soporte en el orquestador (`TranslationOrchestrator`) para reanudar trabajos a partir de su manifiesto, recuperando archivos parciales de la carpeta del trabajo y omitiendo páginas procesadas con éxito.
- Opción `--resume-job <id>` en la interfaz de línea de comandos para reanudar trabajos directamente.
- Soporte nativo para conversiones a DOCX, PDF y ODT de forma local (C#).
- Configuraciones dinámicas de extensiones de entrada y formatos de salida en config.json.
- Modo de prueba CLI `--test-conversion` para validación rápida del conversor de formatos.
- Visor de trabajos (`JobViewerDialog`) que muestra los metadatos JSON y estados de error mediante un árbol de directorios directamente en la TUI, e incluye la exportación manual a Markdown (`[V]`).
- Prompt interactivo de Análisis de Errores al fallar un trabajo en consola, el cual recopila errores OCR y de traducción y sugiere modificaciones empleando el asistente LLM (Prompt Helper).

### Changed
- Reestructurada la interfaz principal de la TUI incorporando navegación lateral por pestañas ("Traductor" e "Historial de Trabajos").
- Modificado `CommandLineOptions` para aceptar el identificador de reanudación y omitir la validación de archivos de entrada cuando se retoma un trabajo.
- Eliminada la generación y escritura de archivos intermedios `.md` (live-writing) en tiempo real, migrando la persistencia de los extractores a retornos asíncronos y almacenamiento exclusivo en los archivos de estado `manifest.json` mediante metadatos.
- Implementada lógica de reintento dinámico no recursivo para fallos de traducción no relacionados a red, escalando gradualmente la temperatura del LLM desde su valor inicial hasta un límite seguro máximo de `0.6`.
- Refactorizado el orquestador monolítico `TranslationOrchestrator.cs` aplicando el Principio de Responsabilidad Única (SRP), extrayendo la persistencia de manifiestos a `JobManifestService.cs`, la gestión de modelos y memoria a `ModelManagerService.cs` y la interactividad de consola a `ConsoleErrorAnalysisService.cs`.

- Corregida la captura de teclas de configuración (barra espaciadora, T, M, P) y la tecla Enter en la TUI al usar la abstracción Key de Terminal.Gui v2 en lugar de comparaciones directas con KeyCode.
- Habilitada la navegación de foco con la tecla Tab en la TUI estableciendo CanFocus = true en los contenedores genéricos principales (_tabContainer, _translatorView, _jobsHistoryView), permitiendo el acceso e interacción con el listado de archivos y demás controles.
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
