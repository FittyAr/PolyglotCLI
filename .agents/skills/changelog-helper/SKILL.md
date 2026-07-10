---
name: changelog-helper
description: Guide AI agents in documenting changes in CHANGELOG.md following the "Keep a Changelog" standard.
---

# Changelog Helper Skill

Use this skill whenever you perform code modifications, add new features, or fix bugs, to ensure that user-facing changes are correctly documented in [CHANGELOG.md](../../../CHANGELOG.md).

## Keep a Changelog Standards

The project follows the [Keep a Changelog v1.1.0](https://keepachangelog.com/en/1.1.0/) format.

All modifications must be listed under the `## [Unreleased]` section in [CHANGELOG.md](../../../CHANGELOG.md). Do not create new version sections unless explicitly requested or during a release process.

## Classification of Changes

Categorize your changes using one of the following subheadings under `## [Unreleased]`:

- **`Added`**: For new features (e.g. new extraction formats, dialog parameters, new options).
- **`Changed`**: For changes in existing functionality (e.g. refactoring UI elements, renaming parameters, upgrading dependencies).
- **`Deprecated`**: For soon-to-be removed features.
- **`Removed`**: For now removed features.
- **`Fixed`**: For bug fixes (e.g. fixing PDF rendering crashes, correcting API timeout exceptions, fixing path issues).
- **`Improved`**: For performance enhancements, UX polish, or styling improvements.

## Rules for Adding Entries

1. **Be Concise and Clear**: Write short, descriptive bullet points explaining *what* changed from the user's perspective.
2. **Use Plain Language**: Avoid overly technical programming jargon (e.g. write "Timeout configuration dialog fixed" instead of "fixed async Task timeout cancellation in SettingsDialog.cs").
3. **Consistency**: Start each bullet point with a capitalized letter, and end with a period.

## Example

```markdown
## [Unreleased]

### Added

- Added support for ODT file extraction.

### Fixed

- Output folder path is now properly validated before saving markdown output.
```
