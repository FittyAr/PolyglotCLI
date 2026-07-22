# Developer Guidelines for AI Agents (`AGENTS.md`)

This document establishes the architecture, design principles, and guidelines for any AI agent or developer modifying or extending the **PolyglotCLI** codebase. All changes must adhere strictly to these rules to maintain high modularity, testability, and cross-platform compatibility.

---

## 1. Core Principles

### Single Responsibility Principle (SRP) & Modularity
* **Rule:** Each class and source file (`.cs`) must perform exactly one, well-defined task. Monolithic "god files" or "god classes" are strictly prohibited.
* **Reasoning:** Prevents files from growing into unmaintainable giants. Makes code review, testing, and AI-driven modifications easy.
* **Example:** `TranslationOrchestrator` orchestrates the pipeline, but delegates document extraction to implementations of `IDocumentExtractor` (e.g., `PdfDocumentExtractor`, `DocxDocumentExtractor`), OCR processing to `OcrService`, translation to `TranslatorService`, and writing output to `MarkdownWriter`.

### Zero Hardcoding
* **Rule:** No magic numbers, default file paths, key names, user-facing strings, or configurations may be hardcoded in the core application logic.
* **Implementation:** 
  * App configurations must be loaded and saved via the `AppConfig` class from `config.json`.
  * System/user prompts for the LLM processing (OCR, translation, review, prompt optimization) must be loaded dynamically from the `prompts` directory via the `PromptLoader` service.

### Extensibility (Open/Closed Principle)
* **Rule:** Support for new file formats or translation clients must be added by implementing interfaces (e.g., `IDocumentExtractor`) and registering them in the factory (`DocumentExtractorFactory`), without modifying the existing execution loops.

---

## 2. Directory & Module Structure

The project follows a modular C# structure. Ensure that new classes are placed in their correct namespaces and directories:

```text
PolyglotCLI/
├── config.json                 # JSON configuration settings
├── CHANGELOG.md                # Project changelog
├── PolyglotCLI.slnx            # .NET Solution file
├── .agents/
│   └── AGENTS.md               # This file
├── PolyglotCLI.core/           # Shared core business logic
│   ├── Clients/                # External API client wrappers (Ollama, Gemini, etc.)
│   ├── Configuration/          # App configuration and processing states
│   │   ├── AppConfig.cs        # Configuration model & persistence handler
│   │   └── PageProcessState.cs # Processing states for document pages
│   └── Services/               # Core business logic services
│       ├── AppLogger.cs        # Custom Serilog-based logging system
│       ├── IDocumentExtractor.cs # Base interface for document extractors
│       ├── DocumentExtractorFactory.cs # Factory to resolve extractors based on extension
│       ├── PdfDocumentExtractor.cs # PDF extraction orchestration (direct text or image OCR)
│       ├── PdfTextExtractor.cs # PDF text extractor using PdfPig
│       ├── PdfPageRenderer.cs  # PDF page image rendering using PDFtoImage/SkiaSharp
│       ├── DocxDocumentExtractor.cs # DOCX text extractor
│       ├── DocDocumentExtractor.cs # DOC text extractor
│       ├── OdtDocumentExtractor.cs # ODT text extractor
│       ├── PlainTextDocumentExtractor.cs # Plain text (txt/md/etc.) extractor
│       ├── ImageDocumentExtractor.cs # Image extractor (direct OCR)
│       ├── OcrService.cs       # Manages vision API requests for OCR
│       ├── TranslatorService.cs # Manages text translation requests
│       ├── TextChunker.cs      # Logic to chunk text by character length
│       ├── MarkdownWriter.cs   # File writer appending translations incrementally
│       ├── TranslationOrchestrator.cs # Main pipeline orchestrating OCR & translation
│       ├── PromptLoader.cs     # Service to load prompt templates from files
│       └── PromptHelperService.cs # Services to optimize, improve and validate prompts
├── PolyglotCLI.web/            # Blazor Web App (presentation and dashboard)
│   ├── Program.cs              # Web app startup and configuration
│   ├── TranslationSession.cs   # In-memory user translation sessions
│   ├── Components/             # UI Components (Pages, Config, Layout)
│   │   ├── Config/             # Settings Tabs (General, OCR, Prompts, etc.)
│   │   ├── Pages/              # Pages: Home, History, Config
│   │   └── Layout/             # Main layout & side navigation
│   └── wwwroot/                # Static assets (CSS, JS)
├── prompts/                    # External prompt files
│   ├── ocr_prompt.md           # Prompt for OCR parsing
│   ├── translation_prompt.md   # Prompt for Translation
│   ├── review_prompt.md        # Prompt for translation review/validation
│   ├── prompt_helper.md        # Prompt for prompt helper/diagnostics
│   └── prompt_improver_prompt.md # Prompt for improving custom prompts
└── docs/                       # Diagrams and documentation
```

---

## 3. Technology Stack & Selected Libraries

Do not implement standard functionality from scratch. Use these pre-selected libraries:

1. **Web UI Components:** `Radzen.Blazor` (component library for Blazor Web Apps).
2. **Logging:** `Serilog`, `Serilog.Sinks.File`, and `Serilog.Sinks.Map` (for dynamic, per-process logging).
3. **PDF Utilities:** `UglyToad.PdfPig` (PDF text extraction) and `PDFtoImage` with `SkiaSharp` (PDF page to image rendering).
4. **Markdown Utilities:** `Markdig` (Markdown parser).
5. **Serialization:** `System.Text.Json` (High performance JSON serialization/deserialization).

---

## 4. Coding Patterns & Constraints

### Configuration Persistence Pattern
* App configurations must be loaded and saved via:
  ```csharp
  var config = AppConfig.Load();
  // ... update configuration ...
  config.Save();
  ```
  Do not parse or write JSON manually; keep the structure aligned in `AppConfig.cs`.

### Asynchronous Operations
* Standardize on C# `async`/`await` for all networking and file I/O operations (HTTP requests to LM Studio, large document reading).
* Always name async methods with the `Async` suffix (e.g., `TranslateAsync`).

### Cross-Platform Path Handling
* Always use `System.IO.Path.Combine` to join paths.
* Never hardcode path separators (use `/` or `\\`).

### Strict Dead Code & Warning Policy
* **Rule:** Never use `#pragma warning disable` or similar mechanisms to silence compiler warnings about unused fields, variables, or obsolete code, unless explicitly justified.
* **Requirement:** Resolve any warnings by implementing the required logic or cleanly removing dead code.

---

## 5. Verification Requirements

Before submitting code, verify:
1. **Compilation:** Run `dotnet build` to ensure the project compiles cleanly with no errors and minimal warnings.
2. **Formatting:** Maintain standard C# guidelines (PascalCase for classes, interfaces prefixed with `I`, camelCase for local parameters/variables).
3. **Execution:** Ensure the web application runs correctly with `dotnet run --project PolyglotCLI.web`.

---

## 6. Required Agent Customizations & Skills

To ensure consistent project maintenance, the agent must check and apply the relevant skills located in the `.agents/skills/` directory.

* **Documentation & Changelog updates:** After any code modification, feature addition, or bug fix, you **must** load and follow the instructions in the `changelog-helper` skill at [skills/changelog-helper/SKILL.md](skills/changelog-helper/SKILL.md) to log your changes in [CHANGELOG.md](../CHANGELOG.md) before concluding.
* **Prompt changes:** When adding or modifying LLM prompt behavior or prompt files, you **must** load and follow the instructions in the `prompts-helper` skill at [skills/prompts-helper/SKILL.md](skills/prompts-helper/SKILL.md).
* **Configuration & Settings changes:** When adding or modifying configuration settings, you **must** load and follow the instructions in the `settings-helper` skill at [skills/settings-helper/SKILL.md](skills/settings-helper/SKILL.md).

---

## 7. Idioma de los Artefactos de Planificación

Los siguientes artefactos generados por el agente durante el proceso de planificación y ejecución de tareas **SIEMPRE deben estar redactados en español**, sin excepción:

| Artefacto | Nombre de archivo típico |
|-----------|--------------------------|
| **Plan de Implementación** (`implementation_plan.md`) | `implementation_plan.md` |
| **Lista de Tareas** (`task.md` / `task_list.md` / `task_plan.md`) | `task.md` |
| **Resumen de Cambios** (`walkthrough.md`) | `walkthrough.md` |

* El español debe aplicarse tanto al contenido narrativo como a los títulos de secciones, descripciones de cambios, preguntas abiertas y notas de verificación.
* El código fuente, nombres de variables, comandos de terminal y fragmentos de código permanecen en inglés (como es estándar en el desarrollo de software).
* Las alertas Markdown (p. ej. `> [!NOTE]`, `> [!IMPORTANT]`) y los bloques de código se redactan con su contenido en español, pero manteniendo la sintaxis técnica en inglés.
