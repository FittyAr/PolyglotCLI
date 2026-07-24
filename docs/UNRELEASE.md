# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added an interactive export modal dialog that lets users customize the destination directory and select which document formats to generate using checkboxes.
- Added IFolderPickerService to pick folders using native OS dialogs in desktop/MAUI mode.

### Improved

- Improved the job details dialog so the page verifier stage (Re-procesar + image/text) fills the dialog height instead of leaving empty space below.

### Changed

- Replaced the `Cropper.Blazor` dependency with `BlazorPanzoom` (shaigem/BlazorPanzoom, MIT) for the "Original (PDF / Imagen)" tab of the page verifier. Pan/zoom is now handled by wrapping the rendered image in a `<Panzoom>` component, eliminating the heavyweight CropperJS canvas overlay while keeping the same gestures (drag, wheel, double-click) and toolbar buttons (Acercar / Alejar / Restablecer).
- Replaced the `PolyglotCLI Web` text in the top bar with the `Square150x150Logo.png` brand mark from `assets/msix/Assets/` (also copied to `PolyglotCLI.Maui/wwwroot/` so MAUI Hybrid serves it the same way).

### Deprecated

### Removed

- Removed the `Cropper.Blazor` NuGet package and its JS/CSS interop layer (`_content/Cropper.Blazor/*`) from both `PolyglotCLI.web` and `PolyglotCLI.Maui`.

### Fixed

- Fixed translation textarea height in the "Verificador de Páginas" tab so it fills the available vertical space instead of staying small.
