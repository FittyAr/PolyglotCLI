using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PolyglotCLI
{
    public static class TranslationOrchestrator
    {
        public static string? CurrentJobDirectory { get; set; }
        public static Action<string, int, bool, string?>? OnPageOcrCompleted { get; set; }

        public static string GetJobsDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "PolyglotCLI", "jobs");
            }
            return Path.Combine(AppContext.BaseDirectory, "jobs");
        }

        public static async Task<int> ExecuteAsync(CommandLineOptions options, AppConfig config)
        {
            // 0. Setup Job Directory
            string timestamp = !string.IsNullOrEmpty(options.ResumeJobId) ? options.ResumeJobId : DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string jobDir = Path.Combine(GetJobsDirectory(), timestamp);

            if (!Directory.Exists(jobDir))
            {
                Directory.CreateDirectory(jobDir);
            }

            CurrentJobDirectory = jobDir;

            // Re-initialize logger to write to the job directory
            AppLogger.Initialize(config, jobDir);

            // Load or initialize manifest
            string manifestPath = Path.Combine(jobDir, "manifest.json");
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
                    JobId = timestamp,
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
                foreach (var target in options.DocumentTargets)
                {
                    currentManifest.Files.Add(new JobFileManifest
                    {
                        SourceFilePath = target.FilePath,
                        FileName = Path.GetFileName(target.FilePath),
                        TargetLanguage = options.TargetLanguage
                    });
                }
                
                currentManifest.Save(manifestPath);
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

            OnPageOcrCompleted = (filePath, pageNum, success, error) =>
            {
                UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, success, error);
            };

            try
            {

            var totalPipelineStopwatch = Stopwatch.StartNew();
            using var systemScope = AppLogger.BeginProcess("System");
            AppLogger.Info("==================================================");
            AppLogger.Info("         STARTING TRANSLATION PIPELINE            ");
            AppLogger.Info("==================================================");
            AppLogger.Info($"Target Language: {options.TargetLanguage}");
            AppLogger.Info($"Output Directory: {options.OutputDirectory}");
            AppLogger.Info($"Debug Mode: {options.Debug}");
            AppLogger.Info($"API URL: {options.ApiUrl}");
            AppLogger.Info($"Translation Model: {options.ModelName}");
            AppLogger.Info($"Vision/OCR Model: {options.VisionModelName}");
            AppLogger.Info($"Files to process: {string.Join(", ", options.Files)}");

            // 1. Load Prompts
            string ocrPrompt;
            string translationPrompt;
            try
            {
                AppLogger.Info("Loading prompt files...");
                var promptLoader = new PromptLoader();
                ocrPrompt = promptLoader.LoadOcrPrompt();
                translationPrompt = promptLoader.LoadTranslationPrompt();
                if (!string.IsNullOrWhiteSpace(options.AdditionalPrompt))
                {
                    translationPrompt += "\n\nAdditional instructions from user:\n" + options.AdditionalPrompt;
                    AppLogger.Debug($"Appended Additional Prompt ({options.AdditionalPrompt.Length} chars).");
                }
                AppLogger.InfoConsole("Loaded prompts successfully.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                AppLogger.ErrorConsole("Failed to load prompt files.", ex);
                return 1;
            }

            // 2. Initialize LM Studio Client and detect models
            AppLogger.Info($"Connecting and validating API server at: {options.ApiUrl}");
            using var checkClient = new LmStudioClient(options.ApiUrl, config.ModelCheckTimeoutSeconds);
            
            string loadedModel = await checkClient.GetFirstLoadedModelAsync();
            if (string.IsNullOrWhiteSpace(loadedModel))
            {
                AppLogger.WarnConsole("Warning: Could not detect any loaded models in LM Studio.");
                AppLogger.Info("Please ensure LM Studio is running, local server is started, and a model is loaded.");
            }
            else
            {
                AppLogger.InfoConsole($"Detected loaded model in backend: {loadedModel}", ConsoleColor.Cyan);
            }

            // Configure services
            // Use specified model names, or fallback to the loaded model
            string textModel = options.ModelName ?? loadedModel;
            string visionModel = options.VisionModelName ?? loadedModel;

            bool requiresVisionModel = false;
            foreach (var target in options.DocumentTargets)
            {
                string ext = Path.GetExtension(target.FilePath).ToLowerInvariant();
                bool isImage = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff";
                if (target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) || isImage)
                {
                    requiresVisionModel = true;
                    break;
                }
            }

            string firstRequiredModel = requiresVisionModel ? visionModel : textModel;
            if (!string.IsNullOrEmpty(firstRequiredModel))
            {
                AppLogger.Info($"VRAM Management: Cleaning up loaded models except '{firstRequiredModel}'...");
                await checkClient.UnloadAllExceptAsync(firstRequiredModel);
            }

            // Initialize separate clients for OCR and Translation processes
            using var ocrClient = new LmStudioClient(options.ApiUrl, config.OcrTimeoutSeconds);
            ocrClient.Temperature = config.OcrTemperature;

            using var translatorClient = new LmStudioClient(options.ApiUrl, config.TranslationTimeoutSeconds);
            translatorClient.Temperature = config.Temperature;

            var ocrService = new OcrService(ocrClient, ocrPrompt, visionModel);
            var translatorService = new TranslatorService(translatorClient, translationPrompt, textModel, options.TargetLanguage);
            translatorService.PreserveFormat = config.PreserveFormat;
            var pageRenderer = new PdfPageRenderer();
            var translatedWriter = new MarkdownWriter();
            var originalWriter = new MarkdownWriter();

            // Initialize Review Service if enabled
            ReviewService? reviewService = null;
            using var reviewClient = options.Verify ? new LmStudioClient(options.ApiUrl, config.ReviewTimeoutSeconds) : null;
            if (options.Verify && reviewClient != null)
            {
                try
                {
                    var promptLoader2 = new PromptLoader();
                    string reviewPrompt = promptLoader2.LoadReviewPrompt();
                    string reviewModel = config.ReviewModel ?? textModel;
                    
                    reviewClient.Temperature = config.ReviewTemperature;
                    reviewService = new ReviewService(reviewClient, reviewPrompt, reviewModel);
                    AppLogger.InfoConsole($"Post-translation review enabled (model: {reviewModel}).", ConsoleColor.Cyan);
                }
                catch (Exception revEx)
                {
                    AppLogger.WarnConsole($"Warning: Could not load review prompt, review disabled: {revEx.Message}");
                }
            }

            // Create output directory if it doesn't exist
            string absoluteOutputDir = Path.GetFullPath(options.OutputDirectory);
            if (!Directory.Exists(absoluteOutputDir))
            {
                AppLogger.Info($"Creating output directory: {absoluteOutputDir}");
                Directory.CreateDirectory(absoluteOutputDir);
            }

            // 3. Phase 1: Text Extraction & OCR for all files/pages
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 1: TEXT EXTRACTION & OCR            ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);
            
            var documentStateCache = new Dictionary<string, List<PageProcessState>>();
            var extractorFactory = new DocumentExtractorFactory();

            {
                using var extractionScope = AppLogger.BeginProcess("Extraction");
                if (options.Debug)
                {
                    AppLogger.WarnConsole("DEBUG MODE ACTIVE: Limiting processing to first 2 pages/chunks.");
                    foreach (var t in options.DocumentTargets)
                    {
                        t.PageRange = "1-2";
                    }
                }

                foreach (var target in options.DocumentTargets)
                {
                    string filePath = target.FilePath;
                    string fileName = Path.GetFileName(filePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                    string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");
                    string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

                    // If resuming, copy back files from job directory if they are missing in output directory
                    if (!string.IsNullOrEmpty(options.ResumeJobId))
                    {
                        string jobOutputPath = Path.Combine(jobDir, Path.GetFileName(outputPath));
                        string jobOriginalOutputPath = Path.Combine(jobDir, Path.GetFileName(originalOutputPath));
                        
                        if (!File.Exists(outputPath) && File.Exists(jobOutputPath))
                        {
                            File.Copy(jobOutputPath, outputPath, true);
                            AppLogger.Info($"Restored partial output file from job directory: {outputPath}");
                        }
                        if (!File.Exists(originalOutputPath) && File.Exists(jobOriginalOutputPath))
                        {
                            File.Copy(jobOriginalOutputPath, originalOutputPath, true);
                            AppLogger.Info($"Restored partial original file from job directory: {originalOutputPath}");
                        }
                    }

                    // Check if file is already completed in manifest
                    var fileM = currentManifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                    if (fileM != null && fileM.Completed)
                    {
                        AppLogger.InfoConsole($"File already completed in past attempts: {fileName}. Skipping.", ConsoleColor.Green);
                        continue;
                    }

                    AppLogger.InfoConsole($"\nProcessing file: {fileName} (Mode: {target.Mode.ToUpperInvariant()})", ConsoleColor.Cyan);
                    
                    try
                    {
                        var extractor = extractorFactory.GetExtractor(filePath);
                        AppLogger.Info($"Running extractor {extractor.GetType().Name} for {fileName}");
                        
                        // Parse existing output files to see what is already processed
                        var cachedStates = new List<PageProcessState>();
                        var originalPages = MarkdownWriter.ReadPages(originalOutputPath);
                        var translatedPages = MarkdownWriter.ReadPages(outputPath);

                        var allPages = new HashSet<int>(originalPages.Keys);
                        allPages.UnionWith(translatedPages.Keys);

                        foreach (var pageNum in allPages)
                        {
                            var state = new PageProcessState { PageNumber = pageNum };
                            
                            if (originalPages.TryGetValue(pageNum, out var ocrText))
                            {
                                state.OcrText = ocrText;
                                state.OcrFailed = string.IsNullOrEmpty(ocrText) || ocrText.StartsWith("*Failed to", StringComparison.OrdinalIgnoreCase);
                                if (state.OcrFailed)
                                {
                                    state.OcrErrorMessage = "Loaded failed OCR page from disk";
                                }
                            }
                            else
                            {
                                state.OcrFailed = true;
                            }

                            if (translatedPages.TryGetValue(pageNum, out var transText))
                            {
                                state.TranslatedText = transText;
                                state.TranslationFailed = string.IsNullOrEmpty(transText) || transText.StartsWith("*Failed to", StringComparison.OrdinalIgnoreCase);
                                if (state.TranslationFailed)
                                {
                                    state.TranslationErrorMessage = "Loaded failed translation page from disk";
                                }
                            }
                            else
                            {
                                state.TranslationFailed = true;
                            }

                            cachedStates.Add(state);
                        }

                        List<PageProcessState> pageStates;
                        if (options.Transcribe)
                        {
                            originalWriter.InitializeOrKeep(originalOutputPath, fileName, "Original");
                            pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer, cachedStates, originalWriter);
                            
                            int successCount = 0;
                            foreach (var s in pageStates)
                            {
                                if (!s.OcrFailed) successCount++;
                            }
                            AppLogger.InfoConsole($"Extracted {successCount}/{pageStates.Count} pages/chunks successfully.", ConsoleColor.Green);
                            
                            // Mark all extracted pages as completed in the manifest
                            foreach (var s in pageStates)
                            {
                                UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, s.PageNumber, !s.OcrFailed, s.OcrFailed ? s.OcrErrorMessage : null);
                            }

                            int textLength = 0;
                            foreach (var s in pageStates)
                            {
                                textLength += s.OcrText?.Trim().Length ?? 0;
                            }
                            AppLogger.Debug($"Total extracted text length for {fileName}: {textLength} characters.");

                            if (textLength == 0 && target.Mode.Equals("text", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                AppLogger.WarnConsole($"[WARNING] No selectable text was found in '{fileName}'.");
                                AppLogger.Warn("Scanned document warning: document appears to contain images only. User should run OCR mode.");
                            }
                        }
                        else
                        {
                            pageStates = cachedStates;
                            AppLogger.InfoConsole($"Transcribe skipped. Loaded {pageStates.Count} pages/chunks from cache.", ConsoleColor.Yellow);
                        }
                        
                        // Filter pageStates by target.PageRange if specified
                        if (!string.IsNullOrWhiteSpace(target.PageRange) && !target.PageRange.Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            int maxPage = 0;
                            foreach (var state in pageStates)
                            {
                                if (state.PageNumber > maxPage) maxPage = state.PageNumber;
                            }
                            var pageFilter = CommandLineOptions.ParsePageRange(target.PageRange, maxPage);
                            if (pageFilter != null)
                            {
                                pageStates = pageStates.FindAll(s => pageFilter.Contains(s.PageNumber));
                                AppLogger.Info($"Filtered page states to range '{target.PageRange}'. Active pages: {string.Join(", ", pageStates.ConvertAll(s => s.PageNumber))}");
                            }
                        }
                        
                        documentStateCache[filePath] = pageStates;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.ErrorConsole($"Fatal error extracting text from {fileName}", ex);
                        
                        var failedStates = new List<PageProcessState>
                        {
                            new PageProcessState 
                            { 
                                PageNumber = 1, 
                                OcrFailed = true, 
                                OcrErrorMessage = ex.Message,
                                OcrText = $"*Failed to read file due to fatal error: {ex.Message}*" 
                            }
                        };
                        documentStateCache[filePath] = failedStates;
                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, 1, false, ex.Message);
                    }
                }
            }

            // Phase 2: Model Transition
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 2: MODEL TRANSITION                 ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);
            if (requiresVisionModel && textModel != visionModel)
            {
                AppLogger.InfoConsole($"Transitioning models: Unloading OCR Vision model ({visionModel}) to free VRAM...", ConsoleColor.Cyan);
                try
                {
                    bool unloaded = await translatorClient.UnloadModelAsync(visionModel);
                    if (unloaded)
                    {
                        AppLogger.InfoConsole($"Successfully unloaded model '{visionModel}'.", ConsoleColor.Green);
                    }
                    else
                    {
                        AppLogger.Info($"Model '{visionModel}' was not active in LM Studio or could not be found.");
                    }
                }
                catch (Exception unloadEx)
                {
                    AppLogger.WarnConsole($"Warning: Failed to unload model '{visionModel}': {unloadEx.Message}");
                }
            }
            else
            {
                AppLogger.Info($"No transition needed. Model '{textModel}' is loaded for translation.");
            }

            // Phase 3: Translation & Markdown Saving
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 3: TRANSLATION & SAVING             ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);

            foreach (var target in options.DocumentTargets)
            {
                string filePath = target.FilePath;
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");
                string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

                // Check if file is already completed in manifest
                var fileM = currentManifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (fileM != null && fileM.Completed)
                {
                    continue;
                }

                AppLogger.InfoConsole($"\nTranslating: {fileName}", ConsoleColor.Cyan);

                if (!documentStateCache.TryGetValue(filePath, out var pageStates) || pageStates.Count == 0)
                {
                    AppLogger.WarnConsole($"Skipping translation for {fileName}: No extracted pages/chunks found.");
                    continue;
                }

                try
                {
                    AppLogger.Debug($"Initializing output files: {outputPath} and {originalOutputPath}");
                    if (options.Transcribe)
                    {
                        originalWriter.InitializeOrKeep(originalOutputPath, fileName, "Original");
                    }
                    if (options.Translate || options.Verify)
                    {
                        translatedWriter.InitializeOrKeep(outputPath, fileName, options.TargetLanguage);
                    }

                    if (options.Translate)
                    {
                        using var translationScope = AppLogger.BeginProcess("Translation");
                        foreach (var state in pageStates)
                        {
                            int pageNum = state.PageNumber;

                            // Check manifest first if resuming
                            var pageM = fileM?.Pages.Find(p => p.PageNumber == pageNum);
                            if (pageM != null && pageM.TranslationCompleted && !string.IsNullOrEmpty(state.TranslatedText) && !state.TranslationFailed)
                            {
                                AppLogger.Info($"Page {pageNum}: Translation already completed.");
                                translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText);
                                continue;
                            }

                            if (!state.TranslationFailed && !string.IsNullOrEmpty(state.TranslatedText))
                            {
                                AppLogger.Info($"Page {pageNum}: Using cached translation from disk.");
                                translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText);
                                UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                continue;
                            }

                            if (state.OcrFailed)
                            {
                                AppLogger.Warn($"Skipping page {pageNum}: OCR/Extraction failed. Propagating error.");
                                state.TranslatedText = state.OcrText;
                                state.TranslationFailed = true;
                                state.TranslationErrorMessage = state.OcrErrorMessage;
                                UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, false, state.OcrErrorMessage);
                            }
                            else
                            {
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                    UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                }
                                catch (Exception transEx)
                                {
                                    // Fix layout: print a newline first to separate from the unfinished Console.Write inside TranslatorService
                                    Console.WriteLine();
                                    AppLogger.ErrorConsole($"Translation Error on page/chunk {pageNum}", transEx);
                                    
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    state.TranslatedText = $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*";
                                    UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, false, transEx.Message);
                                }
                            }

                            originalWriter.SaveOrUpdatePage(pageNum, state.OcrText ?? string.Empty);
                            translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText ?? string.Empty);
                        }
                    }
                    else
                    {
                        AppLogger.InfoConsole("Translation skipped.", ConsoleColor.Yellow);
                    }

                    // Phase 4: Retry Loop for Failed Pages of this file
                    int maxRetries = 3;
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        var failedPages = new List<PageProcessState>();
                        foreach (var state in pageStates)
                        {
                            if ((options.Transcribe && state.OcrFailed) || (options.Translate && state.TranslationFailed))
                            {
                                failedPages.Add(state);
                            }
                        }

                        if (failedPages.Count == 0)
                        {
                            break;
                        }

                        AppLogger.WarnConsole($"\n[RETRY] Attempt {attempt} of {maxRetries} for {failedPages.Count} failed pages/chunks in {fileName}...");
                        await Task.Delay(2000);

                        foreach (var state in failedPages)
                        {
                            int pageNum = state.PageNumber;

                            if (state.OcrFailed)
                            {
                                using var retryOcrScope = AppLogger.BeginProcess("Extraction");
                                AppLogger.Info($"[RETRY] Page/Chunk {pageNum}: Retrying extraction...");
                                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                                if (ext == ".pdf" && target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        byte[] pngBytes = pageRenderer.RenderPageToPng(filePath, pageNum);
                                        state.OcrText = await ocrService.PerformOcrAsync(pngBytes, pageNum);
                                        state.OcrFailed = false;
                                        state.OcrErrorMessage = null;
                                        originalWriter.SaveOrUpdatePage(pageNum, state.OcrText ?? string.Empty);
                                        AppLogger.Info($"[RETRY] Page {pageNum}: Extraction successful on retry.");
                                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                    }
                                    catch (Exception ocrEx)
                                    {
                                        Console.WriteLine(); // fix line break
                                        AppLogger.ErrorConsole($"[RETRY] Page {pageNum} OCR extraction retry failed", ocrEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = ocrEx.Message;
                                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, false, ocrEx.Message);
                                    }
                                }
                                else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff")
                                {
                                    try
                                    {
                                        byte[] imgBytes = File.ReadAllBytes(filePath);
                                        state.OcrText = await ocrService.PerformOcrAsync(imgBytes, 1);
                                        state.OcrFailed = false;
                                        state.OcrErrorMessage = null;
                                        originalWriter.SaveOrUpdatePage(pageNum, state.OcrText ?? string.Empty);
                                        AppLogger.Info($"[RETRY] Image OCR retry successful.");
                                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                    }
                                    catch (Exception imgEx)
                                    {
                                        Console.WriteLine(); // fix line break
                                        AppLogger.ErrorConsole($"[RETRY] Image OCR retry failed", imgEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = imgEx.Message;
                                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, false, imgEx.Message);
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        var extractor = extractorFactory.GetExtractor(filePath);
                                        var reExtracted = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer);
                                        var matched = reExtracted.Find(s => s.PageNumber == pageNum);
                                        if (matched != null && !matched.OcrFailed)
                                        {
                                            state.OcrText = matched.OcrText;
                                            state.OcrFailed = false;
                                            state.OcrErrorMessage = null;
                                            originalWriter.SaveOrUpdatePage(pageNum, state.OcrText ?? string.Empty);
                                            AppLogger.Info($"[RETRY] Page {pageNum}: Re-extraction successful.");
                                            UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                        }
                                        else
                                        {
                                            UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, false, "Re-extraction returned failed or empty result.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AppLogger.ErrorConsole($"[RETRY] Re-extraction of {fileName} failed", ex);
                                        UpdatePageOcrInManifest(currentManifest, manifestPath, filePath, pageNum, false, ex.Message);
                                    }
                                }
                            }

                            if (!state.OcrFailed && state.TranslationFailed)
                            {
                                using var retryTransScope = AppLogger.BeginProcess("Translation");
                                AppLogger.Info($"[RETRY] Page/Chunk {pageNum}: Retrying translation...");
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                    state.TranslationErrorMessage = null;
                                    translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText ?? string.Empty);
                                    AppLogger.Info($"[RETRY] Page {pageNum}: Translation successful on retry.");
                                    UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, true, null);
                                }
                                catch (Exception transEx)
                                {
                                    Console.WriteLine(); // fix line break
                                    AppLogger.ErrorConsole($"[RETRY] Page/Chunk {pageNum} translation retry failed", transEx);
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    translatedWriter.SaveOrUpdatePage(pageNum, $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*");
                                    UpdatePageTranslationInManifest(currentManifest, manifestPath, filePath, pageNum, false, transEx.Message);
                                }
                            }
                        }
                    }

                    // Phase 4.5: Post-Translation Review (if enabled)
                    if (reviewService != null)
                    {
                        using var reviewScope = AppLogger.BeginProcess("Review");
                        AppLogger.InfoConsole($"\n[REVIEW] Running post-translation review for {fileName}...", ConsoleColor.Cyan);

                        foreach (var state in pageStates)
                        {
                            if (state.OcrFailed || state.TranslationFailed)
                            {
                                continue;
                            }

                            // Check review completed in manifest
                            var pageM = fileM?.Pages.Find(p => p.PageNumber == state.PageNumber);
                            if (pageM != null && pageM.ReviewCompleted && !string.IsNullOrEmpty(state.TranslatedText))
                            {
                                AppLogger.Info($"[REVIEW] Page {state.PageNumber}: Review already completed.");
                                continue;
                            }

                            try
                            {
                                string reviewed = await reviewService.ReviewTranslationAsync(
                                    state.OcrText ?? string.Empty,
                                    state.TranslatedText ?? string.Empty,
                                    state.PageNumber
                                );
                                state.ReviewedText = reviewed;
                                state.ReviewFailed = false;

                                state.TranslatedText = reviewed;
                                translatedWriter.SaveOrUpdatePage(state.PageNumber, reviewed);
                                UpdatePageReviewInManifest(currentManifest, manifestPath, filePath, state.PageNumber, true, null);
                            }
                            catch (Exception revEx)
                            {
                                Console.WriteLine(); // fix line break
                                AppLogger.WarnConsole($"[REVIEW] Page {state.PageNumber} review failed (keeping current translation): {revEx.Message}");
                                UpdatePageReviewInManifest(currentManifest, manifestPath, filePath, state.PageNumber, false, revEx.Message);
                            }
                        }
                    }

                    // Print final status for this file
                    var finalFailed = new List<int>();
                    foreach (var state in pageStates)
                    {
                        if ((options.Transcribe && state.OcrFailed) || 
                            (options.Translate && state.TranslationFailed) || 
                            (options.Verify && state.ReviewFailed))
                        {
                            finalFailed.Add(state.PageNumber);
                        }
                    }

                    if (finalFailed.Count > 0)
                    {
                        AppLogger.ErrorConsole($"\nCompleted translating {fileName} with errors in pages/chunks: {string.Join(", ", finalFailed)}");
                    }
                    else
                    {
                        AppLogger.InfoConsole($"\nSuccessfully completed translating {fileName} (all pages/chunks succeeded)!", ConsoleColor.Green);
                        
                        // Mark file completed in manifest
                        var fileManifest = currentManifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                        if (fileManifest != null)
                        {
                            fileManifest.Completed = true;
                            currentManifest.Save(manifestPath);
                        }
                    }

                    if (File.Exists(originalOutputPath))
                    {
                        AppLogger.Info($"Original text saved to: {originalOutputPath}");
                    }
                    if (File.Exists(outputPath))
                    {
                        AppLogger.Info($"Output saved to: {outputPath}");
                    }

                    // Phase 5: Output Format Conversion
                    if (options.GenerateDoc && !string.IsNullOrWhiteSpace(options.SelectedFormat))
                    {
                        using var conversionScope = AppLogger.BeginProcess("Conversion");
                        AppLogger.InfoConsole($"Converting output to {options.SelectedFormat.ToUpperInvariant()}...", ConsoleColor.Cyan);
                        try
                        {
                            if (File.Exists(outputPath))
                            {
                                await OutputFormatConverter.ConvertToFormatsAsync(outputPath, options.SelectedFormat);
                            }
                            if (File.Exists(originalOutputPath))
                            {
                                await OutputFormatConverter.ConvertToFormatsAsync(originalOutputPath, options.SelectedFormat);
                            }
                            UpdateFileConversionInManifest(currentManifest, manifestPath, filePath, true);
                        }
                        catch (Exception convEx)
                        {
                            AppLogger.ErrorConsole($"Conversion Error", convEx);
                            UpdateFileConversionInManifest(currentManifest, manifestPath, filePath, false);
                        }
                    }

                    // Save copies of markdown files to job directory
                    if (!string.IsNullOrEmpty(CurrentJobDirectory))
                    {
                        try
                        {
                            if (File.Exists(outputPath))
                            {
                                File.Copy(outputPath, Path.Combine(CurrentJobDirectory, Path.GetFileName(outputPath)), true);
                            }
                            if (File.Exists(originalOutputPath))
                            {
                                File.Copy(originalOutputPath, Path.Combine(CurrentJobDirectory, Path.GetFileName(originalOutputPath)), true);
                            }
                        }
                        catch (Exception copyEx)
                        {
                            AppLogger.Warn($"Failed to copy final Markdown files to job directory: {copyEx.Message}");
                        }
                    }

                    // Clean up markdown files if not requested
                    if (!config.SaveMarkdown)
                    {
                        AppLogger.Info("Cleaning up temporary Markdown files...");
                        try
                        {
                            if (File.Exists(outputPath)) File.Delete(outputPath);
                            if (File.Exists(originalOutputPath)) File.Delete(originalOutputPath);
                        }
                        catch (Exception cleanEx)
                        {
                            AppLogger.Warn($"Failed to clean up Markdown files: {cleanEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.ErrorConsole($"Fatal Error during translation processing for {fileName}", ex);
                }
            }

            // Determine overall status
            bool overallSuccess = true;
            foreach (var fileM in currentManifest.Files)
            {
                if (!fileM.Completed)
                {
                    overallSuccess = false;
                    break;
                }
            }

            currentManifest.Status = overallSuccess ? "Completed" : "Failed";
            currentManifest.Save(manifestPath);

            totalPipelineStopwatch.Stop();
            AppLogger.Info("==================================================");
            AppLogger.Info($"Translation Process Finished in {totalPipelineStopwatch.Elapsed.TotalSeconds:F2} seconds.");
            AppLogger.Info("==================================================");
            return overallSuccess ? 0 : 1;
            }
            catch (Exception pipelineEx)
            {
                currentManifest.Status = "Failed";
                currentManifest.Save(manifestPath);
                AppLogger.ErrorConsole("Pipeline Execution Failed with Exception", pipelineEx);
                return 1;
            }
            finally
            {
                OnPageOcrCompleted = null;
            }
        }

        private static void UpdatePageOcrInManifest(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    FileName = Path.GetFileName(filePath),
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

        private static void UpdatePageTranslationInManifest(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    FileName = Path.GetFileName(filePath),
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

        private static void UpdatePageReviewInManifest(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    FileName = Path.GetFileName(filePath),
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

        private static void UpdateFileConversionInManifest(JobManifest manifest, string manifestPath, string filePath, bool success)
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
    }
}
