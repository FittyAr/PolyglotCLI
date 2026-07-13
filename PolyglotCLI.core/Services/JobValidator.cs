using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PolyglotCLI
{
    public static class JobValidator
    {
        public static (bool IsValid, string? ErrorMessage) ValidateJobSettings(AppConfig config, string scanDir)
        {
            // 1. LM Studio API URL
            string apiUrl = config.ApiUrl;
            if (string.IsNullOrEmpty(apiUrl))
            {
                return (false, "LM Studio API URL cannot be empty.");
            }
            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uriResult) || 
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "LM Studio API URL must be a valid HTTP or HTTPS URL (e.g., http://localhost:1234/v1).");
            }

            // 2. Translation Model Name
            string translationModel = config.DefaultModel ?? "";
            if (string.IsNullOrEmpty(translationModel))
            {
                return (false, "Translation Model Name cannot be empty.");
            }

            // 3. Target Language
            string targetLang = config.TargetLanguage ?? "";
            if (string.IsNullOrEmpty(targetLang))
            {
                return (false, "Target Language cannot be empty.");
            }
            if (!Regex.IsMatch(targetLang, @"^[a-zA-Z\s\-]+$"))
            {
                return (false, "Target Language must contain only letters, spaces, or hyphens (e.g., 'English' or 'Brazilian-Portuguese').");
            }

            // 4. Output Directory
            string outDir = config.OutputDirectory ?? "";
            if (string.IsNullOrEmpty(outDir))
            {
                return (false, "Output Directory cannot be empty.");
            }
            char[] invalidChars = Path.GetInvalidPathChars();
            if (outDir.IndexOfAny(invalidChars) >= 0)
            {
                return (false, "Output Directory contains invalid path characters.");
            }

            // 5. Directory to Scan
            if (string.IsNullOrEmpty(scanDir))
            {
                return (false, "Directory to Scan cannot be empty.");
            }
            if (!Directory.Exists(scanDir))
            {
                return (false, $"The directory to scan '{scanDir}' does not exist on the system.");
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
                    return (false, "Vision/OCR Model Name cannot be empty.\nIt is required to translate images or PDFs in OCR mode.");
                }
            }
            return (true, null);
        }
    }
}
