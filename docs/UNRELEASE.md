# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Improved

- Improved Job Details modal layout to be collapsible and fluidly resizable on smaller monitors.
- Improved system translation, review, and OCR prompts with industry-standard terminology guidelines, formatting preservation (no-linting rules), local path mapping, and localized typographical punctuation.
- Enhanced prompt engineering helper guides for users, adding structured glossary examples, regional localization instructions, and SEO considerations.

### Changed

### Deprecated

### Removed

### Fixed

- Fixed write permission errors when exporting jobs or saving configurations while running inside write-protected directories like Program Files. The outputs and modified configurations are now redirected to the safe user AppData directory.
