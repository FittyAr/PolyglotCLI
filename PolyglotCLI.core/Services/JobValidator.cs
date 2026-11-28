using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PolyglotCLI.Localization;
using PolyglotCLI.Validation;

namespace PolyglotCLI
{
    public static class JobValidator
    {
        public static (bool IsValid, string? ErrorMessage) ValidateJobSettings(AppConfig config, string scanDir)
        {
            // 1. LM Studio API URL — delega a NetworkUrlValidator para
            // no duplicar la lógica (esquema, host vacío, longitud
            // máxima, etc.). El validator es la única fuente de verdad.
            var urlResult = NetworkUrlValidator.SanitizeApiUrl(config.ApiUrl);
            if (!urlResult.IsValid)
            {
                return (false, CoreText.Format("Core.InvalidApiUrl", "Invalid API URL: {0} (value: '{1}').", urlResult.FirstError, config.ApiUrl));
            }

            // 2. Translation Model Name
            string translationModel = config.DefaultModel ?? "";
            if (string.IsNullOrEmpty(translationModel))
            {
                return (false, CoreText.Get("Core.TranslationModelRequired", "The translation model name cannot be empty."));
            }

            // 3. Target Language
            string targetLang = config.TargetLanguage ?? "";
            if (string.IsNullOrEmpty(targetLang))
            {
                return (false, CoreText.Get("Core.TargetLanguageRequired", "The target language cannot be empty."));
            }
            if (!Regex.IsMatch(targetLang, @"^[\p{L}\s\-]+$"))
            {
                return (false, CoreText.Get("Core.TargetLanguageInvalid", "The target language can contain only letters, spaces or hyphens (e.g. 'English' or 'Brazilian-Portuguese')."));
            }

            // 4. Output Directory — chequeo mínimo de chars no
            // válidos. La validación completa (path traversal, control
            // chars, length) ya corre en AppConfig.Save via
            // FileSystemPathValidator; acá solo necesitamos detectar
            // chars que el FS rechaza antes de pasarle al usuario un
            // mensaje críptico.
            string outDir = config.OutputDirectory ?? "";
            if (string.IsNullOrEmpty(outDir))
            {
                return (false, CoreText.Get("Core.OutputDirectoryRequired", "The output folder cannot be empty."));
            }
            char[] invalidChars = Path.GetInvalidPathChars();
            if (outDir.IndexOfAny(invalidChars) >= 0)
            {
                return (false, CoreText.Get("Core.OutputDirectoryInvalidChars", "The output folder contains invalid path characters."));
            }

            // 5. Directory to Scan
            if (string.IsNullOrEmpty(scanDir))
            {
                return (false, CoreText.Get("Core.ScanDirectoryRequired", "The folder to scan cannot be empty."));
            }
            if (!Directory.Exists(scanDir))
            {
                return (false, CoreText.Format("Core.ScanDirectoryNotFound", "The folder to scan '{0}' does not exist.", scanDir));
            }

            return (true, null);
        }

        public static (bool IsValid, string? ErrorMessage) ValidateOcrModelRequirement(AppConfig config, List<SelectableFile> files)
        {
            string visionModel = config.DefaultVisionModel ?? "";
            if (string.IsNullOrEmpty(visionModel))
            {
                bool needsVision = false;
                foreach (var f in files)
                {
                    if (f.IsSelected)
                    {
                        string ext = Path.GetExtension(f.FullPath).ToLowerInvariant();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff" ||
                            (ext == ".pdf" && f.Mode.Equals("image", StringComparison.OrdinalIgnoreCase)))
                        {
                            needsVision = true;
                            break;
                        }
                    }
                }

                if (needsVision)
                {
                    return (false, CoreText.Get("Core.VisionModelRequired", "The vision/OCR model name cannot be empty.\nIt is required to translate images or PDFs in OCR mode."));
                }
            }
            return (true, null);
        }

        public static (bool IsValid, string? ErrorMessage) ValidatePromptImprovementSettings(AppConfig config, string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput))
            {
                return (false, CoreText.Get("Core.AdditionalPromptRequired", "Write some text in the additional translation instructions box first."));
            }
            if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.DefaultModel))
            {
                return (false, CoreText.Get("Core.ApiNotConfigured", "Configure the API URL and the translation model in Settings first."));
            }
            return (true, null);
        }

        public static (bool IsValid, string? ErrorMessage) ValidateFileAnalysisSettings(AppConfig config, int selectedIndex, int filesCount)
        {
            if (selectedIndex < 0 || selectedIndex >= filesCount)
            {
                return (false, CoreText.Get("Core.SelectFileToAnalyze", "Select a file in the documents list to analyze."));
            }
            if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.DefaultModel))
            {
                return (false, CoreText.Get("Core.ApiNotConfigured", "Configure the API URL and the translation model in Settings first."));
            }
            return (true, null);
        }
    }
}
