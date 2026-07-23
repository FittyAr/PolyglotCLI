# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Soporte adicional para generar el instalador .exe con **Nullsoft Install System (NSIS 3)** (`installer/PolyglotCLI.nsi`, `scripts/build_nsis_installer.ps1`, `scripts/install_nsis.ps1`). Replica la funcionalidad de Inno Setup: instalador grafico en espanol con asistente MUI 2, jerarquia `Server\serverapp` y `Desktop\desktopapp`, soporte multi-usuario, accesos directos a Servidor, Web y Escritorio en el Menu Inicio (y Escritorio con checkbox independiente marcado por defecto), registro en `Programas y caracteristicas` con `DisplayName`, `DisplayVersion`, `Publisher`, `EstimatedSize`, `InstallLocation` y `UninstallString`, e icono corporativo reutilizando `assets/icons/app.ico` y los PNG del manifiesto convertidos a BMP bajo `assets/nsis/`. La salida se publica como `PolyglotCLI-<version>-x64-nsis-setup.exe` en `artifacts/dist/` y se sube al GitHub Release junto al instalador Inno Setup y al MSIX. El workflow `release.yml` instala NSIS via Chocolatey y anade el paso `Build Setup EXE Installer (NSIS)` despues del de Inno. La nueva opcion 8 de `run.ps1` permite compilar y abrir el instalador NSIS localmente.

### Improved

### Changed

### Deprecated

### Removed

### Fixed
