using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class JobManifestService
    {
        public static void InitializeJobDirectory(string jobDir, AppConfig config)
        {
            if (!Directory.Exists(jobDir))
            {
                Directory.CreateDirectory(jobDir);
            }

            // Save config copy
            try
            {
                config.Save(Path.Combine(jobDir, "config.json"));
            }
            catch (Exception cfgEx)
            {
                AppLogger.Warn($"Failed to save config.json copy to job directory: {cfgEx.Message}");
            }
        }

        public static JobManifest LoadOrInitializeManifest(
            string jobDir, 
            CommandLineOptions options, 
            AppConfig config, 
            string manifestPath)
        {
            JobManifest currentManifest;
            if (!string.IsNullOrEmpty(options.ResumeJobId) && File.Exists(manifestPath))
            {
                currentManifest = JobManifest.Load(manifestPath);
                
                string jobConfigPath = Path.Combine(jobDir, "config.json");
                if (File.Exists(jobConfigPath))
                {
                    try
                    {
                        var jobConfig = AppConfig.Load(jobConfigPath);
                        config.TranslationTimeoutSeconds = jobConfig.TranslationTimeoutSeconds;
                        config.OcrTimeoutSeconds = jobConfig.OcrTimeoutSeconds;
                        config.ReviewTimeoutSeconds = jobConfig.ReviewTimeoutSeconds;
                        config.Temperature = jobConfig.Temperature;
                        config.OcrTemperature = jobConfig.OcrTemperature;
                        config.ReviewTemperature = jobConfig.ReviewTemperature;
                        config.MaxCharactersPerChunk = jobConfig.MaxCharactersPerChunk;
                        config.ChunkOverlapCharacters = jobConfig.ChunkOverlapCharacters;
                        config.PreserveFormat = jobConfig.PreserveFormat;
                        config.EnableReview = jobConfig.EnableReview;
                        config.ReviewModel = jobConfig.ReviewModel;
                        AppLogger.Info($"Loaded custom configuration settings from job copy of config.json.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to load config.json copy from job directory: {ex.Message}");
                    }
                }

                // Override options with manifest values so the run is consistent!
                options.Mode = currentManifest.Mode;
                options.TargetLanguage = currentManifest.TargetLanguage;
                options.OutputDirectory = currentManifest.OutputDirectory;
                options.PageRange = currentManifest.PageRange;
                options.ModelName = currentManifest.ModelName;
                options.VisionModelName = currentManifest.VisionModelName;
                options.AdditionalPrompt = currentManifest.AdditionalPrompt;
                options.Transcribe = currentManifest.Transcribe;
                options.Translate = currentManifest.Translate;
                options.Verify = currentManifest.Verify;
                options.GenerateDoc = currentManifest.GenerateDoc;
                options.SelectedFormat = currentManifest.SelectedFormat;
                
                // Rebuild files list and targets
                options.Files.Clear();
                options.DocumentTargets.Clear();
                foreach (var fileM in currentManifest.Files)
                {
                    options.Files.Add(fileM.SourceFilePath);
                    options.DocumentTargets.Add(new DocumentTarget
                    {
                        FilePath = fileM.SourceFilePath,
                        Mode = currentManifest.Mode,
                        PageRange = currentManifest.PageRange
                    });
                }
                
                AppLogger.Info($"Resuming past job: '{options.ResumeJobId}'");
            }
            else
            {
                currentManifest = new JobManifest
                {
                    JobId = Path.GetFileName(jobDir),
                    CreatedAt = DateTime.Now,
                    LastUpdatedAt = DateTime.Now,
                    Status = "InProgress",
                    TargetLanguage = options.TargetLanguage,
                    Mode = options.Mode,
                    OutputDirectory = options.OutputDirectory,
                    PageRange = options.PageRange,
                    ModelName = options.ModelName,
                    VisionModelName = options.VisionModelName,
                    AdditionalPrompt = options.AdditionalPrompt,
                    Transcribe = options.Transcribe,
                    Translate = options.Translate,
                    Verify = options.Verify,
                    GenerateDoc = options.GenerateDoc,
                    SelectedFormat = options.SelectedFormat
                };
                
                // Populate files in manifest
                string sourcesDir = Path.Combine(jobDir, "sources");
                if (!Directory.Exists(sourcesDir))
                {
                    Directory.CreateDirectory(sourcesDir);
                }

                foreach (var target in options.DocumentTargets)
                {
                    string originalFileName = Path.GetFileName(target.FilePath);
                    string normalizedFileName = Regex.Replace(originalFileName, @"[^a-zA-Z0-9_\-\.]", "");
                    string copiedFilePath = Path.Combine(sourcesDir, normalizedFileName);
                    
                    try
                    {
                        if (target.FilePath != copiedFilePath)
                            File.Copy(target.FilePath, copiedFilePath, true);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to copy {originalFileName} to sources directory: {ex.Message}");
                    }

                    target.FilePath = copiedFilePath;

                    currentManifest.Files.Add(new JobFileManifest
                    {
                        SourceFilePath = copiedFilePath,
                        OriginalFileName = originalFileName,
                        NormalizedFileName = normalizedFileName,
                        CopiedFilePath = copiedFilePath,
                        TargetLanguage = options.TargetLanguage
                    });
                }
                
                currentManifest.Save(manifestPath);
            }

            return currentManifest;
        }

        public static void UpdatePageOcr(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.OcrCompleted = true;
                page.OcrError = null;
            }
            else
            {
                page.OcrCompleted = false;
                page.OcrError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdatePageTranslation(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.TranslationCompleted = true;
                page.TranslationError = null;
            }
            else
            {
                page.TranslationCompleted = false;
                page.TranslationError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdatePageReview(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.ReviewCompleted = true;
                page.ReviewError = null;
            }
            else
            {
                page.ReviewCompleted = false;
                page.ReviewError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdateFileConversion(JobManifest manifest, string manifestPath, string filePath, bool success)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                foreach (var page in file.Pages)
                {
                    page.ConversionCompleted = success;
                }
                manifest.Save(manifestPath);
            }
        }

        public static void SavePageStatesToJson(List<PageProcessState> pageStates, string dataJsonPath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(dataJsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dataList = new List<DocumentPageData>();
                foreach (var state in pageStates)
                {
                    dataList.Add(new DocumentPageData
                    {
                        PageNumber = state.PageNumber,
                        OriginalText = state.OcrText,
                        TranslatedText = state.TranslatedText,
                        ReviewedText = state.ReviewedText,
                        IsOcrSuccessful = !state.OcrFailed,
                        IsTranslationSuccessful = !state.TranslationFailed,
                        OcrErrorMessage = state.OcrErrorMessage,
                        TranslationErrorMessage = state.TranslationErrorMessage
                    });
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(dataList, options);
                File.WriteAllText(dataJsonPath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to save JSON state to {dataJsonPath}: {ex.Message}");
            }
        }

        public static List<JobManifest> LoadPastJobs()
        {
            var pastJobs = new List<JobManifest>();
            string jobsDir = TranslationOrchestrator.GetJobsDirectory();
            if (!Directory.Exists(jobsDir))
            {
                return pastJobs;
            }

            try
            {
                var dirs = Directory.GetDirectories(jobsDir);
                foreach (var dir in dirs)
                {
                    string manifestPath = Path.Combine(dir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = JobManifest.Load(manifestPath);
                        if (manifest != null && !string.IsNullOrEmpty(manifest.JobId))
                        {
                            pastJobs.Add(manifest);
                        }
                    }
                }
                
                // Sort jobs descending by JobId (newest first)
                pastJobs.Sort((a, b) => string.Compare(b.JobId, a.JobId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Error scanning past jobs: {ex.Message}");
            }
            return pastJobs;
        }

        public static List<string> GetJobDataFiles(string jobDir)
        {
            var files = new List<string>();
            string dataDir = Path.Combine(jobDir, "data");
            if (Directory.Exists(dataDir))
            {
                files.AddRange(Directory.GetFiles(dataDir, "*_data.json"));
            }
            foreach (var file in Directory.GetFiles(jobDir, "*_data.json"))
            {
                if (!files.Contains(file)) files.Add(file);
            }
            return files;
        }

        public static List<DocumentPageData> GetJobDataPages(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<DocumentPageData>>(json) ?? new List<DocumentPageData>();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Failed to load job data pages from {filePath}: {ex.Message}");
                }
            }
            return new List<DocumentPageData>();
        }

        public static string BuildErrorSummary(JobManifest manifest)
        {
            var sbErr = new System.Text.StringBuilder();
            foreach (var f in manifest.Files)
            {
                foreach (var p in f.Pages)
                {
                    if (!p.OcrCompleted && !string.IsNullOrEmpty(p.OcrError))
                        sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: OCR, Error: {p.OcrError}");
                    if (!p.TranslationCompleted && !string.IsNullOrEmpty(p.TranslationError))
                        sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: Translation, Error: {p.TranslationError}");
                }
            }
            return sbErr.ToString();
        }
    }
}
