# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Soporte nativo para conversiones a DOCX, PDF y ODT de forma local (C#).
- Configuraciones dinámicas de extensiones de entrada y formatos de salida en config.json.
- Modo de prueba CLI `--test-conversion` para validación rápida del conversor de formatos.

### Fixed
- Corregido error en el que las respuestas vacías devueltas por los modelos de traducción o revisión sobrescribían el contenido traducido original.
- Corregidos errores gráficos y de congelamiento/superposición de pantalla de la TUI en la terminal al salir, inicializando y disponiendo de forma limpia la instancia `IApplication` de `Terminal.Gui` v2.

### Changed
- Integración de las librerías open-source HtmlToOpenXml, PeachPDF y NetOdt como fallback local cuando pandoc no está disponible.
- Actualización de los menús interactivos y de ajustes para consumir dinámicamente los formatos de salida soportados.
- Adaptación de la configuración de agentes (`AGENTS.md`) de Rust/Pairee a C#/.NET 10/PolyglotCLI.
- Adaptación de las herramientas de asistencia (skills) `settings-helper` y `changelog-helper` al contexto del proyecto.
- Reemplazo de la habilidad `localize-helper` por `prompts-helper` adaptada al sistema de prompts del proyecto.
- Migración del almacenamiento de configuraciones, temporales, logs y archivos resultantes al directorio `%appdata%\PolyglotCLI` en sistemas Windows.
- Actualización completa de la interfaz gráfica de terminal (TUI) para adaptarla a los cambios de la versión 2.4.17 de `Terminal.Gui`.
- Modularización completa del archivo monolítico `InteractiveMenu.cs` mediante una clase parcial no estática, organizando el diseño, eventos, acciones y diálogos asistentes en archivos independientes bajo el directorio `Services/InteractiveMenu/`.
