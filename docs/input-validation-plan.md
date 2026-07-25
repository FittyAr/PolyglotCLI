# Plan: Centralized Input Validation

**Status**: draft for review
**Scope**: defense-in-depth for all user-controlled inputs that touch
filesystem operations, HTTP requests, or the LLM prompt.

## Background

`commit efb3eab` added `JobManifestService.IsValidJobId()` to defend
against path traversal via hand-edited manifests. That fix was a
**point fix** for one vector. A complete audit (this document) shows
many other vectors with the same shape: user-controlled string that
gets concatenated into a filesystem path, URL, HTTP header, or LLM
prompt. The same defense pattern (validate at the boundary, reject
or sanitize) needs to be applied systematically.

The risk model: PolyglotCLI is a desktop app where the user controls
their own `config.json`, can edit `manifest.json` files by hand, and
can navigate to URLs with crafted query strings. There is no
multi-tenant auth boundary, so the threat model is "user attacks
themselves" (e.g. via a typo or a botched hand-edit) and
"defense-in-depth against future imports/integrations that bring
untrusted data into the app".

## Architecture (per .NET fullstack skill)

- **Single Responsibility**: one validator class per input type.
- **Clean Architecture**: validators live in `PolyglotCLI.core` (no UI
  dependencies), so they're reusable from any entry point.
- **ValidationResult<T>**: a small wrapper that carries the sanitized
  value, success flag, and list of error messages. Pure functions.
- **Testable**: each validator is a pure function with a dedicated
  test class.

```
PolyglotCLI.core/
└── Validation/
    ├── ValidationResult.cs            generic result type
    ├── FileSystemPathValidator.cs     file/dir/extension/path-traversal
    ├── NetworkUrlValidator.cs         URLs, SSRF defense, scheme check
    ├── ModelNameValidator.cs          LLM model + provider names
    ├── PromptValidator.cs             prompt text + injection heuristics
    └── NumericRangeValidator.cs       timeouts, temperatures, chunk sizes
```

## Vectors (audit results)

### 1. Filesystem (HIGHEST)

| Source field | Where it lands | Risk |
|---|---|---|
| `AppConfig.OutputDirectory` | `Path.Combine`, `Directory.CreateDirectory` | path traversal → write outside jobs root |
| `AppConfig.LastScanDirectory` | `Path.Combine` (file browser) | path traversal → enumerate arbitrary dirs |
| `AppConfig.LogDirectory` | `Path.Combine` (logger init) | path traversal → log to arbitrary path |
| `JobManifest.OutputDirectory` | `Path.Combine` (export) | path traversal |
| `JobFileManifest.SourceFilePath` | `File.ReadAllText`, `File.Copy` | path traversal → read arbitrary files |
| `JobFileManifest.CopiedFilePath` | `File.Move`, `File.Delete` | path traversal → modify/delete arbitrary files |
| `ExportJobDialog.exportDirectory` | `Directory.CreateDirectory` | path traversal |
| `Home.razor.currentDirectory` | `Path.Combine` (file browser) | path traversal |
| CLI args: `--input`, `--output`, `--scan` | `CommandLineOptions` → `Path.Combine` | path traversal |

### 2. Network URLs (HIGH)

| Source field | Where it lands | Risk |
|---|---|---|
| `AppConfig.ApiUrl` | `HttpClient.BaseAddress` (via `LlmClientFactory`) | SSRF → probe internal network (10.x, 192.168.x, localhost) |
| `ProviderConfig.ApiUrl` (per-provider override) | same | same |
| `ProviderApiKeys[*]` dict keys | lookup | invalid provider name → exception |

### 3. Model names (MEDIUM)

| Source field | Where it lands | Risk |
|---|---|---|
| `AppConfig.DefaultModel` | `LlmClient.SendTextRequestAsync` | injection in request URL/header |
| `AppConfig.DefaultVisionModel` | same | same |
| `AppConfig.ReviewModel` | `ReviewService` | same |
| `JobManifest.ModelName` | LLM API call | same |
| `JobManifest.VisionModelName` | LLM API call | same |
| `JobManifest.SelectedFormat` | `OutputFormatConverter` | invalid format → crash |

### 4. Provider names (MEDIUM)

| Source field | Where it lands | Risk |
|---|---|---|
| `AppConfig.Provider` | `LlmProviderHelper.ParseProvider` | invalid enum → crash / wrong client |
| `AppConfig.OcrProvider`, `TranslationProvider`, `ReviewProvider` | same | same |

### 5. Prompts (LOW for security, MEDIUM for UX)

| Source field | Where it lands | Risk |
|---|---|---|
| `AppConfig.AdditionalPrompt` | appended to LLM prompt | huge strings → DoS; prompt injection in error-analysis flows |
| `JobManifest.AdditionalPrompt` | same | same |
| `JobPageManifest.OcrError` / `TranslationError` / `ReviewError` | included in error-analysis prompt | untrusted content (from LLM failures) → injection |

### 6. Numeric values (LOW for security, but DoS)

| Source field | Where it lands | Risk |
|---|---|---|
| `TranslationTimeoutSeconds`, etc. | `HttpClient.Timeout` | 0 → instant timeout, negative → undefined |
| `Temperature`, `OcrTemperature`, `ReviewTemperature` | LLM client | out-of-range values → model failure |
| `MaxCharactersPerChunk` | text chunking | huge values → memory exhaustion |
| `ChunkOverlapCharacters` | same | overlap > chunk → infinite loop |
| `ModelCheckTimeoutSeconds` | HTTP | tiny values → always fails |

### 7. List fields (LOW)

| Source field | Where it lands | Risk |
|---|---|---|
| `SupportedInputExtensions` | file filter | invalid entries (e.g. `*` or no dot) → filter doesn't match |
| `SupportedOutputFormats` | output converter | invalid formats → crash |
| `ProviderApiKeys`, `ProviderConfigs` dict keys | lookup | invalid keys → miss → silent fallback |

## Validator signatures

```csharp
namespace PolyglotCLI.Validation;

public class ValidationResult<T>
{
    public bool IsValid { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }
    public string? FirstError => Errors.FirstOrDefault();
}

public static class FileSystemPathValidator
{
    // Reject names with path separators, NUL, control chars, or
    // path traversal. Return the same string (no transformation) if
    // valid; otherwise return a sanitized version (replace bad
    // chars with _) plus the error.
    public static ValidationResult<string> SanitizeFileName(string? name);

    // Validate a directory path. If mustExist=true, also check
    // Directory.Exists. Doesn't change the path (callers do that
    // via Path.GetFullPath).
    public static ValidationResult<string> SanitizeDirectoryPath(string? path, bool mustExist = false);

    // Strip leading dot, require leading dot.
    public static ValidationResult<string> SanitizeFileExtension(string? ext);

    public static bool ContainsPathTraversal(string? path);
}

public static class NetworkUrlValidator
{
    // Validate URL, parse to Uri, return it.
    public static ValidationResult<Uri> SanitizeApiUrl(string? url);

    // True for 127.x.x.x, ::1, 10.x, 172.16-31.x, 192.168.x,
    // 169.254.x. PolyglotCLI is local-only by design; any URL
    // pointing to a private IP from a config is suspicious.
    public static bool IsPrivateOrLocalhost(Uri uri);

    // Only allow http and https.
    public static bool HasValidScheme(Uri uri);
}

public static class ModelNameValidator
{
    // LLM model names: alnum, dash, dot, slash, colon, underscore.
    // Reject whitespace, control chars, shell metachars.
    public static ValidationResult<string> SanitizeModelName(string? name);

    // Same but stricter: only alnum + underscore.
    public static ValidationResult<string> SanitizeProviderName(string? name);
}

public static class PromptValidator
{
    // Limit length. Optional injection heuristics.
    public static ValidationResult<string> SanitizePrompt(string? prompt, int maxLength = 50_000);

    // Basic heuristics for prompt-injection patterns:
    // "ignore previous", "system:", "<|im_start|>", etc.
    public static IReadOnlyList<string> DetectInjectionAttempts(string? prompt);
}

public static class NumericRangeValidator
{
    // Clamp timeouts to [1, 3600]. Default if 0 or negative.
    public static int ClampTimeout(int value, int defaultValue = 300);

    // Clamp temperature to [0, 2]. Default 0.3.
    public static double ClampTemperature(double value, double defaultValue = 0.3);

    // Clamp chunk size to [100, 100_000]. Default 6000.
    public static int ClampChunkSize(int value, int defaultValue = 6000);

    // Enforce chunkOverlap < chunkSize to prevent infinite loops.
    public static int ClampChunkOverlap(int value, int chunkSize, int defaultValue = 300);
}
```

## Integration points

### Phase 1: Core validators (no behavior change)

Create the validator classes + tests. No integration yet. ~30-40 unit
tests covering each validator.

Files:
- `PolyglotCLI.core/Validation/ValidationResult.cs`
- `PolyglotCLI.core/Validation/FileSystemPathValidator.cs`
- `PolyglotCLI.core/Validation/NetworkUrlValidator.cs`
- `PolyglotCLI.core/Validation/ModelNameValidator.cs`
- `PolyglotCLI.core/Validation/PromptValidator.cs`
- `PolyglotCLI.core/Validation/NumericRangeValidator.cs`

Tests:
- `PolyglotCLI.test/Validation/FileSystemPathValidatorTests.cs`
- `PolyglotCLI.test/Validation/NetworkUrlValidatorTests.cs`
- `PolyglotCLI.test/Validation/ModelNameValidatorTests.cs`
- `PolyglotCLI.test/Validation/PromptValidatorTests.cs`
- `PolyglotCLI.test/Validation/NumericRangeValidatorTests.cs`

### Phase 2: Service-layer integration (defensive logging)

In each consumer, wrap the existing operation with validation:
**don't break behavior**, but log a warning if invalid input slips
through. This catches real-world bugs without disrupting the user.

Concrete callsites:

- `AppConfig.Save()` → validate `ApiUrl`, `OutputDirectory`,
  `LogDirectory`, `DefaultModel`, `DefaultVisionModel`, `ReviewModel`,
  numeric ranges, list fields. Log warnings, but **don't refuse to
  save** (the existing values may be valid in some edge case the
  validator doesn't know about).
- `JobManifestService.LoadOrInitializeManifest()` → validate
  `JobManifest.OutputDirectory`, `ModelName`, `VisionModelName`,
  `TargetLanguage`. Log warnings.
- `JobManifestService.LoadPastJobs()` → already validates `JobId`
  (existing fix from `efb3eab`).
- `TranslationOrchestrator.ExecuteAsync()` → validate the final
  options before kicking off the pipeline.
- `LlmClientFactory.CreateClient()` → validate `ApiUrl` before
  creating the `HttpClient` (block obvious SSRF attempts).

### Phase 3: UI feedback

Show user-friendly errors when validation fails. This is where
RadzenNotification/Alert come in.

Concrete callsites:

- `Config.razor.cs.SaveConfig()` → validate inputs, show
  `NotificationService.Notify` with the first error if any field
  is invalid. **Don't refuse to save** unless the field is critical
  (e.g. `ApiUrl` is malformed).
- `ExportJobDialog.Export()` → validate `exportDirectory` before
  starting. Block the operation if invalid.
- `Home.razor.cs` file browser `LoadDirectory` → validate
  `currentDirectory` before enumerating. Block if invalid.
- `Home.razor.cs` resume logic → already validates `ResumeJobId`
  (existing fix from `e7b3182`).
- `GeneralConfigTab.razor` → validate `ApiUrl` and `ApiKey` on
  input change. Show inline error if invalid.

### Phase 4: Strict mode (optional, gated by config)

Add a `StrictValidation` bool to `AppConfig` (default false). When
true, validation failures cause operations to **refuse to proceed**
with a hard error, not just a log warning. This gives power users a
"paranoid" mode without breaking workflows for normal users.

## Migration strategy (low risk)

Roll out in 4 separate PRs, one per phase. Each phase is independently
mergeable:

- **PR 1** (Phase 1): validators + tests, no integration. ~500 LoC.
- **PR 2** (Phase 2): service-layer integration with logging only.
  Behavior unchanged, just better diagnostics. ~300 LoC + tests.
- **PR 3** (Phase 3): UI feedback. Notifications for invalid inputs.
  ~400 LoC + tests.
- **PR 4** (Phase 4): strict mode flag. Optional. ~200 LoC + tests.

After all 4 PRs, run `/find-bugs` again to verify the surface has
shrunk.

## Test strategy

For each validator:
- ~5-10 happy-path inputs (typical values)
- ~10-15 attack vectors per vector type (path traversal, SSRF,
  model name injection, prompt injection, numeric overflow)
- ~3-5 edge cases (empty, whitespace, max length, unicode, RTL)

Target: ~80-100 new unit tests across all validators.

## Out of scope (deliberate)

- **SQL injection**: not applicable (no SQL DB).
- **XSS**: not applicable (Blazor auto-escapes; no raw HTML
  rendering from user input). RadzenDialog renders the `Message`
  parameter from server-side, but Blazor escapes by default.
- **CSRF**: Blazor Server has anti-forgery built-in. Not a concern.
- **Authentication/Authorization**: PolyglotCLI is a single-user
  desktop app. No auth boundary yet (could be added later if a
  multi-tenant use case appears).
- **File upload validation (MIME type, content sniffing)**: the file
  browser only opens files, doesn't upload them. Extractor validation
  is a separate concern.
- **Cryptographic validation**: covered by the existing DPAPI
  implementation; no new crypto in this plan.

## Estimated effort

- Phase 1: 4-6 hours (validators + 80-100 unit tests)
- Phase 2: 2-3 hours (service-layer integration + smoke tests)
- Phase 3: 3-4 hours (UI feedback)
- Phase 4: 2-3 hours (strict mode)

Total: ~12-16 hours, naturally split across 4 PRs over a week or
two of focused work.

## Open questions for review

1. **Where to draw the line on prompt-injection detection**?
   Heuristics like "ignore previous" are noisy (false positives on
   legitimate prompts). Option: do length/control-char checks only
   in Phase 1, defer injection heuristics to a later phase if needed.
2. **Strict mode default**: should it be opt-in (default off) or
   opt-out (default on for new installs)? I'd vote opt-in for
   backwards compat.
3. **Provider name whitelist**: should we hardcode the list of
   supported providers (Ollama, LM Studio, etc.) and reject anything
   else, or accept any string and let `LlmProviderHelper` reject it?
   Whitelist is safer but more brittle.

## References

- OWASP Path Traversal: https://owasp.org/www-community/attacks/Path_Traversal
- OWASP SSRF: https://owasp.org/www-community/attacks/Server_Side_Request_Forgery
- OWASP Input Validation Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html
- CWE-22 (Path Traversal), CWE-918 (SSRF), CWE-20 (Improper Input Validation)
