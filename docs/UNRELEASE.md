# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Paquete NuGet [Cropper.Blazor 1.5.1](https://github.com/CropperBlazor/Cropper.Blazor) (`Cropper.Blazor`) referenciado desde `PolyglotCLI.web` y `PolyglotCLI.Maui`. Registra `builder.Services.AddCropper()` en ambos `Program.cs` / `MauiProgram.cs` para configurar el cliente JS de interop interno.
- Historial de Trabajos (Web UI + Desktop MAUI): nuevo botón **Importar trabajo (.zpg)** en la esquina superior derecha de la página `/history`, junto al título, que abre un diálogo con selector de archivo `.zpg`/`.zip` y restaura la carpeta completa del trabajo en `%APPDATA%/PolyglotCLI/jobs/`. Si el `JobId` ya existe localmente el importador lo renombra automáticamente como `{JobId}_imported_{yyyyMMdd_HHmmss}` para evitar sobrescrituras.
- Historial de Trabajos (Web UI + Desktop MAUI): nuevo botón **Exportar .zpg** (icono `archive`, color warning) por cada fila, que descarga la carpeta completa del trabajo como archivo `.zpg` (Zip Polyglot). Para trabajos en estado `InProgress` muestra un diálogo de confirmación explicando que el paquete será parcial e incluirá una nota `PACKAGE_NOTES.txt`.
- Servicio `PolyglotCLI.JobPackageService` (en `PolyglotCLI.core/Services/JobPackageService.cs`) con `ExportJobPackage(jobDir, outputStream)` e `ImportJobPackageAsync(inputStream, jobsRoot)`; maneja el renombrado en conflictos, valida que el ZIP contenga un `manifest.json` reconocible, limpia carpetas temporales de staging y registra la operación en `AppLogger`.
- Endpoints HTTP en el host web (`PolyglotCLI.web/Program.cs`): `GET /api/jobs/{jobId}/package` para descarga con `Content-Disposition: attachment`, y `POST /api/jobs/import` para subida multipart con `IFormFile` en el campo `file` (devuelve `{ jobId }` con el identificador efectivo).
- Abstracción multiplataforma `PolyglotCLI.IJobPackageHost` (`PolyglotCLI.core/Services/IJobPackageHost.cs`) con dos implementaciones: `PolyglotCLI.web.Services.WebJobPackageHost` (usa `NavigationManager` + `DialogService` + `HttpClient` contra los endpoints HTTP) y `PolyglotCLI.Maui.Services.MauiJobPackageHost` (usa `FilePicker.Default.PickAsync` para importar y `CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(...)` para mostrar el diálogo nativo "Guardar como" al exportar). Ambas se registran en su contenedor DI correspondiente (`AddScoped` para web, `AddSingleton` para MAUI), lo que permite que la página `/history` funcione idéntica en ambos modos sin errores de DI por servicios inexistentes en MAUI.

### Improved

- Verificador de Páginas (Web UI): la traducción se muestra ahora renderizada en Markdown mediante el componente `RadzenMarkdown`, con un botón que alterna entre modo Edición (TextArea con guardado automático) y Vista previa.
- Verificador de Páginas (Web UI): los cambios manuales de traducción se rastrean en un diccionario `pendingTranslationEdits` por sesión, garantizando que el guardado del JSON aplique explícitamente todas las páginas editadas, no solo la última visitada. Se añade un icono `edit` naranja junto a cada página modificada en el listado izquierdo y un contador "X página(s) editada(s) en esta sesión" junto al encabezado de la página visualizada.
- Verificador de Páginas (Web + Desktop MAUI): el visor de páginas renderizadas usa el componente `<CropperComponent>` de [Cropper.Blazor](https://github.com/CropperBlazor/Cropper.Blazor) con `DragMode = "move"` y crop-box deshabilitado (`AutoCrop = false`, `CropBoxMovable = false`, `CropBoxResizable = false`, `Modal = false`, `Background = false`, `ToggleDragModeOnDblclick = false`), de modo que se comporta como visor de pan/zoom puro. La barra de control (Acercar/Alejar/Restablecer) usa `<RadzenButton Icon="...">` para garantizar iconos Material Symbols correctos y se sitúa en la parte superior con `position: relative; z-index: 10` para quedar siempre sobre el visor. Funciona junto con los gestos integrados: arrastrar para mover, rueda del ratón para zoom, doble clic para acercar.

### Changed

- Historial de Trabajos (Web + MAUI): el importador de `.zpg` mejora su diagnóstico de error. Antes mostraba únicamente "no contiene un manifest.json en su raíz"; ahora distingue entre archivo corrupto/no-zip, zip vacío, sin estructura de carpeta raíz reconocible (lista las 5 primeras entradas detectadas) y extracción exitosa sin `manifest.json` (lista los archivos encontrados en el directorio extraído).
- Historial de Trabajos (MAUI): el botón Exportar `.zpg` ya no abre el archivo guardado con el diálogo "Abrir con…" del sistema; ahora muestra el diálogo nativo "Guardar como" mediante `CommunityToolkit.Maui.Storage.FileSaver`, permitiendo al usuario elegir carpeta y nombre de archivo en una sola acción.
- Empaquetador/importador de trabajos: la extracción del ZIP se realiza directamente sobre la carpeta de staging (sin re-anidar el prefijo de nivel superior detectado). Esto elimina el bug por el cual un paquete válido quedaba como `{staging}/{jobId}/{jobId}/manifest.json` y el importador reportaba falsamente "no contiene un manifest.json".
- Verificador de Páginas (Web UI): se sustituyó el desplegable de archivos `*_data.json` por una lista visible que muestra ambos documentos a la vez con su nombre original (por ejemplo, "CalificacinDesempolvador.pdf") y el conteo de páginas.
- Verificador de Páginas (Web UI): se redujo el ancho del panel izquierdo de documentos (Size 3 → 2) y se amplió el panel derecho (Size 9 → 10) para aprovechar mejor el espacio.
- Verificador de Páginas (Web UI): la pestaña "Texto Extraído (OCR)" se reubicó a la columna derecha, junto a "Traducción" y "Pensamiento (Reasoning)".

### Deprecated

### Removed

- Librería [panzoom](https://github.com/timmywil/panzoom) de timmywil (`wwwroot/lib/panzoom/panzoom.min.js`) y el módulo JS de interoperabilidad (`wwwroot/js/panzoomInterop.js`) — sustituidos por Cropper.Blazor.
- Toda la lógica JS interop manual del visor (`IJSRuntime`, `OnAfterRenderAsync` que inicializaba `panzoomTarget`, `DisposeAsync`): el componente `<CropperComponent>` la reemplaza por una API tipada en C#.

### Fixed

- Verificador de Páginas (Web + Desktop MAUI): el visor de imágenes se reemplaza por `<CropperComponent>` de Cropper.Blazor, eliminando las inconsistencias previas entre web y desktop en el manejo del pan/zoom manual. La librería está oficialmente soportada en Blazor Web App, WebAssembly, Server, MAUI Blazor Hybrid, MVC.
- Verificador de Páginas (Web + Desktop MAUI): los botones Acercar/Alejar/Restablecer del visor no respondían a clics porque se usaba `<span class="material-icons">` con la fuente de Material Icons no cargada y los botones HTML no estaban renderizando los handlers de forma fiable. Se sustituyen por `<RadzenButton Icon="...">` (que sí carga la tipografía Material Symbols) y los handlers pasan de lambdas `@(() => ZoomAsync(+1))` a métodos `Task`-returning directos `ZoomInClick`/`ZoomOutClick`/`InvokeZoom(int)`/`ResetPageZoom`. La fila de toolbar incluye `position: relative; z-index: 10` para evitar cualquier interceptación por el overlay interno de Cropper.js. Las llamadas al API de Cropper van envueltas en try-catch que emiten `NotificationService` para distinguir entre "clic no llegó al handler" y "el handler falló al llamar la JS API".
- Verificador de Páginas (Web): `MaximumReceiveMessageSize` de SignalR se amplía a 32MB para soportar imágenes base64 grandes transmitidas al cliente por la API JS de Cropper.Blazor.
- Importador de `.zpg` (Web): la subida del archivo `.zpg` se realiza directamente mediante `HttpClient.PostAsync` contra el endpoint `POST /api/jobs/import`, evitando pasar el archivo entero por SignalR y sorteando el límite de `MaximumReceiveMessageSize` del hub.
