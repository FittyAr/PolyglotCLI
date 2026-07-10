---
name: prompts-helper
description: Guide AI agents when adding or modifying LLM system prompts in the prompts directory.
---

# Prompts Helper Skill

Use this skill when you need to introduce new prompt behaviors, modify system prompt templates, or add options to improve prompt outcomes.

## Structure of Prompts

All LLM system and user prompt templates are stored as Markdown/Text files in the [prompts](../../../prompts) directory:

- **`ocr_prompt.md`**: Guide the vision model on how to transcribe PDF page images.
- **`translation_prompt.md`**: Instructions for the translation model to translate text to target languages.
- **`review_prompt.md`**: Instructions for the reviewer model to verify and improve translated text.
- **`prompt_helper.md` / `prompt_improver_prompt.md`**: Prompts used to optimize/improve custom prompts.

These are loaded at runtime by [PromptLoader.cs](../../../Services/PromptLoader.cs).

## Guidelines

1. **Never Hardcode System Prompts in Code**: Do not write system instructions directly inside [TranslationOrchestrator.cs](../../../Services/TranslationOrchestrator.cs) or other C# files.
2. **Modify Markdown files**: Edit the corresponding `.md` file inside the `prompts/` folder.
3. **Format Preservation**: Ensure prompt templates clearly specify that formatting (e.g. Markdown structure, tables, titles) must be preserved in the output.
4. **Validation**: Check that any changes to prompts do not break formatting assumptions made by the parser.
