using System;
using System.IO;

namespace PolyglotCLI
{
    public class PromptLoader
    {
        private readonly string _promptsDirectory;

        public PromptLoader(string? promptsDirectory = null)
        {
            // Default to 'prompts' folder relative to the execution directory
            _promptsDirectory = promptsDirectory ?? Path.Combine(AppContext.BaseDirectory, "prompts");
            
            // If it doesn't exist relative to execution, fallback to project root folder prompts
            if (!Directory.Exists(_promptsDirectory))
            {
                _promptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "prompts");
            }
        }

        public string PromptsDirectory => _promptsDirectory;

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
