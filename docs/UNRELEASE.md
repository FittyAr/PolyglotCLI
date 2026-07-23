# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Improved

### Changed
- Modificado el ejecutable de la aplicación Blazor `PolyglotCLI.web` para iniciarse por defecto en **modo ventana nativo** (tipo MAUI/Desktop) ocultando la consola de comandos, y permitiendo opcionalmente correr en **modo servidor web clásico** pasándole el parámetro `--web`.
- Actualizado el instalador de WiX en `PolyglotCLI.Wix` para utilizar la interfaz estándar interactiva `WixUI_FeatureTree`. Ahora el usuario puede elegir si desea crear un acceso directo opcional en el Escritorio durante el proceso de instalación.
- Configurada la aplicación en `PolyglotCLI.Wix` y `AppxManifest.xml` para empaquetar y utilizar el icono oficial `app.ico` (generado a partir de la imagen corporativa `favicon.png`) en el ejecutable, los accesos directos de Inicio/Escritorio y el Panel de Control.
- Centralizado el icono de la aplicación en [app.ico](file:///d:/GitHub/PolyglotCLI/manifests/app.ico), eliminando las copias redundantes y configurando MSBuild y WiX para referenciar la fuente de verdad única en tiempo de compilación.
- Renombrados y migrados los activos de pantalla de bienvenida del manifiesto MSIX a la nomenclatura estándar de escala de Windows (`SplashScreen.scale-100.png` a `scale-400.png`) para el mapeo DPI dinámico automático en tiempo de ejecución.
- Reescrito el script de instalación [install.ps1](file:///d:/GitHub/PolyglotCLI/scripts/install.ps1) para compilar localmente y ejecutar de forma interactiva el instalador MSI nativo (`PolyglotCLI.Wix.msi`) mediante `msiexec`, eliminando las descargas de archivos ZIP y permitiendo probar localmente el flujo de instalación de WiX idéntico a como se haría desde GitHub Actions.

### Deprecated

### Removed

### Fixed
