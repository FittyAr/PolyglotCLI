# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Añadida comprobación previa e instalación automática desatendida de .NET 10 Desktop Runtime en el instalador Inno Setup (`.exe`).

### Improved

### Changed

### Deprecated

### Removed

### Fixed

- Corregida la compilación del instalador en CI/CD instalando Inno Setup en el runner de GitHub Actions y mejorando la localización dinámica de `ISCC.exe` vía `PATH` y carpetas estándar de Inno Setup 6/7.
- Corregida la desincronización de `UNRELEASE.md` y `CHANGELOG.md` al ejecutar la opción 6 de `run.ps1` (`scripts/bump_version.ps1`). Ahora el script detecta si el número de versión no cambia y revierte automáticamente las modificaciones locales en `csproj`, `CHANGELOG.md` y `UNRELEASE.md` si la operación se cancela o falla.
