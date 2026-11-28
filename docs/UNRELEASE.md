# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added a privacy policy (`docs/PRIVACY.md`), linked from the About tab, and a Microsoft Store submission guide (`docs/STORE_SUBMISSION.md`).
- Added a self-contained MSIX package for the Microsoft Store, with Store logos at every scale.
- The interface is now available in 10 languages: English, Spanish, Portuguese, French, German, Italian, Russian, Simplified Chinese, Japanese and Korean. It follows the Windows display language (English for any other language) and can be changed under Settings > General > Interface language.
- The desktop app now shows a download link when the WebView2 runtime is missing and logs unhandled errors.

### Improved

### Changed

- Upgraded to .NET 11 and the latest library versions (MAUI 11, Radzen.Blazor 13, Semantic Kernel 1.82, CommunityToolkit.Maui 17, xUnit v3, and others).
- The default output folder is now `Documents\PolyglotCLI`.
- Exported `.zpg` packages no longer contain API keys or logs.
- The app version is now defined once in `Directory.Build.props` for both the web and desktop apps.
- The installer now checks the required runtimes per component (.NET 11, plus ASP.NET Core 11 for the server).

### Deprecated

### Removed

- Removed the NetOdt dependency; ODT files are now written internally.
- Removed unused MAUI template resources and the Google Fonts download.

### Fixed

- Cancelling a job now marks it as cancelled and also stops in-flight LLM requests.
- Resuming text-based jobs (TXT, DOCX, ODT, DOC) no longer wipes or re-translates pages that were already translated.
- Several open tabs no longer duplicate the log console or start two translations at once.
- Deleting, resuming or reprocessing jobs, and installing updates, are blocked while a translation is running.
- Editing a page now writes the file with the job's language and output folder, not the current settings.
- ODT export now works in Program Files and Microsoft Store installs.
- Importing large `.zpg` packages (up to 2 GB) no longer fails, and tampered packages can no longer read files outside the job.
- The web server is protected against DNS rebinding and checks request origins strictly.
- Microsoft Store installs never try to update themselves.
- File names with accents no longer collide when a job is created.
- Fixed infinite loops with a chunk size of 0 or huge page ranges.
- The default LLM server URL no longer points to a development IP address.
- A corrupt `config.json` is backed up before defaults are restored.
- "Test connection" no longer reports success when the server is unreachable.
- The `pre-push` git hook now actually runs.
