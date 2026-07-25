# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added DPAPI encryption for API keys stored in `config.json` (`AppConfig.ApiKey`, `ProviderApiKeys[*]`, `ProviderConfigs[*].ApiKey`). Encryption is per-user / per-machine via `System.Security.Cryptography.ProtectedData` (CurrentUser scope). Migrates automatically on the next save: legacy plaintext keys are read as-is and re-encrypted in place.
- Added a `SecureField` helper class with `Protect` / `Unprotect` / `Mask` utilities, plus 14 new unit tests covering the roundtrip, idempotency, legacy-plaintext migration, and the UI mask function.
- Added an `AppConfig.LastMigrationUtc` property (read from the `.migrated` marker) and a yellow banner in the About tab that surfaces the migration date and reminds the user to re-enter any API keys that were discarded by the reset.
- Added a `POLYGLOTCLI_USE_PROJECT_CONFIG` env var that lets developers opt back into the legacy `currentDir` / `baseDir` config lookup (off by default; for dev iteration only).

### Improved

- Improved the "API Key" field in the General config tab so the stored key is never displayed in clear text. The key is shown as `first5…last5` (or fully masked if ≤10 chars) and a separate input is used to replace it. A new "delete" button removes the stored key without prompting for a replacement.
- Added a new "Ubicaciones de archivos" section in the About tab showing the resolved paths for `config.json`, prompts, logs, output directory, and jobs directory, with a per-row "copy to clipboard" button and a reminder that `config.json` stores API keys encrypted with DPAPI.
- `SecureField.Mask` now guarantees at least 8 characters of hidden middle for any non-trivial key. Keys ≤18 chars are fully masked (no info leak on very short API keys).
- `SecureField.UnprotectInPlace` now preserves dictionary entries whose value failed to decrypt (previously dropped silently), tagged with a `__decrypt_failed__` marker so the UI can show "key could not be decrypted" instead of a blank slot. Each failure is logged with the field path (`ProviderApiKeys[OpenAi]`, etc.).
- Dirty tracking on the Config page now includes the prompt text fields, so editing a prompt and navigating away triggers the "unsaved changes" modal (previously could be lost silently).
- MAUI WebView2 user-data folder moved to `%LocalAppData%\FittyAr\PolyglotCLI\WebView2\` for consistency with the rest of the user-data tree.

### Changed

- Standardized all user-data paths under `%AppData%\FittyAr\PolyglotCLI\` (the `{developer}\{program}` convention). This affects `config.json`, the logs directory, the jobs/history directory, the default output directory, and the editable prompts directory. Paths are no longer scattered between the project tree and `%AppData%\PolyglotCLI\`.
- On first run after this change, a one-shot migration moves the legacy `%AppData%\PolyglotCLI\` tree into the new location, preserving `logs/`, `jobs/`, and the bundled `prompts/`. The legacy `config.json` is **discarded** on purpose: the app starts with a blank `config.json` (no API keys, no last-used directories, no custom provider URLs). The migration is idempotent — a `.migrated` marker prevents re-running. The About tab now surfaces the migration date.
- `AppConfig.Load` now prefers the AppData `config.json` over `currentDir` / `baseDir` configs, making AppData the single source of truth for user configuration. Developers can still pass an explicit `configPath` to `Load` for local overrides, or set `POLYGLOTCLI_USE_PROJECT_CONFIG=1` to opt back into the legacy lookup.
- `PromptLoader` now keeps the user-editable prompts in `%AppData%\FittyAr\PolyglotCLI\prompts\` (bootstrapped from the bundle on first run). Edits survive app updates.

### Deprecated

### Removed

### Fixed

- Fixed the "Logs" path in the About tab being shown as `%AppData%\PolyglotCLI\logs\` (the legacy location) — it now correctly points to `%AppData%\FittyAr\PolyglotCLI\logs\`, matching where `AppLogger` actually writes.
- Fixed `SecureField.Mask` revealing 10 of 11 characters for short keys (now fully masks anything ≤18 chars).
- Fixed `AppConfig` losing keys silently when DPAPI decryption failed (e.g., `config.json` copied from another user/machine). Entries are now preserved with a marker and the failure is logged with enough context to act on.
