using System;
using System.IO;

namespace PolyglotCLI
{
    public class PromptLoader
    {
        private readonly string _promptsDirectory;

        public PromptLoader(string? promptsDirectory = null)
        {
            _promptsDirectory = ResolvePromptsDirectory(promptsDirectory);
        }

        public string PromptsDirectory => _promptsDirectory;

        private static string ResolvePromptsDirectory(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath))
            {
                return explicitPath;
            }

            // Lista de candidatos en orden de prioridad. El primero que exista y contenga
            // los archivos de prompt se considera la ubicación canónica.
            string[] candidateRelativePaths = new[]
            {
                "assets/prompts",
                "prompts"
            };

            // 1. Try AppContext.BaseDirectory / <candidate>
            foreach (var rel in candidateRelativePaths)
            {
                string baseDirPrompts = Path.Combine(AppContext.BaseDirectory, rel);
                if (Directory.Exists(baseDirPrompts) && File.Exists(Path.Combine(baseDirPrompts, "translation_prompt.md")))
                {
                    return baseDirPrompts;
                }
            }

            // 2. Try CurrentDirectory / <candidate>
            string currentDirFallback = string.Empty;
            foreach (var rel in candidateRelativePaths)
            {
                string currentDirPrompts = Path.Combine(Directory.GetCurrentDirectory(), rel);
                if (Directory.Exists(currentDirPrompts) && File.Exists(Path.Combine(currentDirPrompts, "translation_prompt.md")))
                {
                    return currentDirPrompts;
                }
                if (string.IsNullOrEmpty(currentDirFallback)) { currentDirFallback = currentDirPrompts; }
            }

            // 3. Walk up parent directories to find the project root assets/prompts/ (o prompts/)
            var searchDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (searchDir != null)
            {
                foreach (var rel in candidateRelativePaths)
                {
                    string candidate = Path.Combine(searchDir.FullName, rel);
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                    {
                        return candidate;
                    }
                }
                searchDir = searchDir.Parent;
            }

            var baseSearchDir = new DirectoryInfo(AppContext.BaseDirectory);
            while (baseSearchDir != null)
            {
                foreach (var rel in candidateRelativePaths)
                {
                    string candidate = Path.Combine(baseSearchDir.FullName, rel);
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                    {
                        return candidate;
                    }
                }
                baseSearchDir = baseSearchDir.Parent;
            }

            return currentDirFallback;
        }

        public string LoadOcrPrompt()
        {
            return LoadPromptFile("ocr_prompt.md");
        }

        public string LoadTranslationPrompt()
        {
            return LoadPromptFile("translation_prompt.md");
        }

        public string LoadReviewPrompt()
        {
            return LoadPromptFile("review_prompt.md");
        }

        public string LoadErrorAnalysisPrompt()
        {
            return LoadPromptFile("error_analysis_prompt.md");
        }

        public string LoadPromptImproverPrompt()
        {
            return LoadPromptFile("prompt_improver_prompt.md");
        }

        public void SaveOcrPrompt(string content)
        {
            SavePromptFile("ocr_prompt.md", content);
        }

        public void SaveTranslationPrompt(string content)
        {
            SavePromptFile("translation_prompt.md", content);
        }

        public void SaveReviewPrompt(string content)
        {
            SavePromptFile("review_prompt.md", content);
        }

        public void SavePromptImproverPrompt(string content)
        {
            SavePromptFile("prompt_improver_prompt.md", content);
        }

        public void SaveErrorAnalysisPrompt(string content)
        {
            SavePromptFile("error_analysis_prompt.md", content);
        }

        private string LoadPromptFile(string filename)
        {
            string fullPath = Path.Combine(_promptsDirectory, filename);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required prompt file '{filename}' was not found in directory '{_promptsDirectory}'. Make sure you run the app from the root directory or copy prompts to the output directory.");
            }

            return File.ReadAllText(fullPath).Trim();
        }

        private void SavePromptFile(string filename, string content)
        {
            if (!Directory.Exists(_promptsDirectory))
            {
                Directory.CreateDirectory(_promptsDirectory);
            }
            string fullPath = Path.Combine(_promptsDirectory, filename);
            File.WriteAllText(fullPath, content ?? string.Empty, System.Text.Encoding.UTF8);
        }
    }
}
