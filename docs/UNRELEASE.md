# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Improved

### Changed

- Reverted the `PolyglotCLI.web` project to a pure web server configuration.
- Migrated native desktop execution to the `PolyglotCLI.Maui` project using Blazor Hybrid.
- Moved `ApplicationMode` execution state to `PolyglotCLI.core` for clean sharing between projects.
- Conditionally hide the layout footer when running in desktop mode, keeping it visible only in web mode.
- Split the app execution command in run.ps1 into separate Desktop and Web options.

### Deprecated

### Removed

### Fixed

- Fixed window initialization crash of the WebView2 components by configuring the application thread mode.
- Resolved MSB3277 WindowsBase reference mismatch warning by enabling WPF framework references in the web project.
