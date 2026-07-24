# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added an interactive export modal dialog that lets users customize the destination directory and select which document formats to generate using checkboxes.
- Added IFolderPickerService to pick folders using native OS dialogs in desktop/MAUI mode.

### Improved

### Changed

### Deprecated

### Removed

### Fixed

- Fixed broken image preview in the "Verificador de Páginas" tab by adding the required `max-height/max-width` CSS rules for the Cropper.Blazor image.
- Fixed translation textarea height in the "Verificador de Páginas" tab so it fills the available vertical space instead of staying small.
