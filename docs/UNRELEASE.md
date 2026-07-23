# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Paquete NuGet [Cropper.Blazor 1.5.1](https://github.com/CropperBlazor/Cropper.Blazor) (`Cropper.Blazor`) referenciado desde `PolyglotCLI.web` y `PolyglotCLI.Maui`. Registra `builder.Services.AddCropper()` en ambos `Program.cs` / `MauiProgram.cs` para configurar el cliente JS de interop interno.

### Improved

- Verificador de Páginas (Web UI): la traducción se muestra ahora renderizada en Markdown mediante el componente `RadzenMarkdown`, con un botón que alterna entre modo Edición (TextArea con guardado automático) y Vista previa.
- Verificador de Páginas (Web UI): los cambios manuales de traducción se rastrean en un diccionario `pendingTranslationEdits` por sesión, garantizando que el guardado del JSON aplique explícitamente todas las páginas editadas, no solo la última visitada. Se añade un icono `edit` naranja junto a cada página modificada en el listado izquierdo y un contador "X página(s) editada(s) en esta sesión" junto al encabezado de la página visualizada.
- Verificador de Páginas (Web + Desktop MAUI): el visor de páginas renderizadas usa el componente `<CropperComponent>` de [Cropper.Blazor](https://github.com/CropperBlazor/Cropper.Blazor) con `DragMode = "move"` y crop-box deshabilitado (`AutoCrop = false`, `CropBoxMovable = false`, `CropBoxResizable = false`, `Modal = false`, `Background = false`, `ToggleDragModeOnDblclick = false`), de modo que se comporta como visor de pan/zoom puro. La barra de control (Acercar/Alejar/Restablecer) usa `<RadzenButton Icon="...">` para garantizar iconos Material Symbols correctos y se sitúa en la parte superior con `position: relative; z-index: 10` para quedar siempre sobre el visor. Funciona junto con los gestos integrados: arrastrar para mover, rueda del ratón para zoom, doble clic para acercar.

### Changed

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
