# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Módulo JS de interoperabilidad `wwwroot/js/panzoomInterop.js` que adapta la librería [panzoom](https://github.com/timmywil/panzoom) (cargada vía CDN en `App.razor`) para inicializar y manipular instancias por elemento DOM desde Blazor mediante `IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/panzoomInterop.js")`.

### Improved

- Verificador de Páginas (Web UI): la traducción se muestra ahora renderizada en Markdown mediante el componente `RadzenMarkdown`, con un botón que alterna entre modo Edición (TextArea con guardado automático) y Vista previa.
- Verificador de Páginas (Web UI): los cambios manuales de traducción se rastrean en un diccionario `pendingTranslationEdits` por sesión, garantizando que el guardado del JSON aplique explícitamente todas las páginas editadas, no solo la última visitada. Se añade un icono `edit` naranja junto a cada página modificada en el listado izquierdo y un contador "X página(s) editada(s) en esta sesión" junto al encabezado de la página visualizada.
- Verificador de Páginas (Web UI): el visor de páginas renderizadas añade interacción de pan/zoom mediante la librería [panzoom](https://github.com/timmywil/panzoom) (cargada por CDN). Se incluye una mini-toolbar flotante con botones Acercar/Alejar/Restablecer y un pie con atajos: arrastrar para mover, rueda del ratón para zoom, doble clic para acercar.

### Changed

- Verificador de Páginas (Web UI): se sustituyó el desplegable de archivos `*_data.json` por una lista visible que muestra ambos documentos a la vez con su nombre original (por ejemplo, "CalificacinDesempolvador.pdf") y el conteo de páginas.
- Verificador de Páginas (Web UI): se redujo el ancho del panel izquierdo de documentos (Size 3 → 2) y se amplió el panel derecho (Size 9 → 10) para aprovechar mejor el espacio.
- Verificador de Páginas (Web UI): la pestaña "Texto Extraído (OCR)" se reubicó a la columna derecha, junto a "Traducción" y "Pensamiento (Reasoning)".

### Deprecated

### Removed

### Fixed
