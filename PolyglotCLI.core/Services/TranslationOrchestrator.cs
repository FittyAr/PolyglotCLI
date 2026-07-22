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
        public static System.Threading.CancellationToken ActiveCancellationToken { get; set; } = System.Threading.CancellationToken.None;

        public static string GetJobsDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "PolyglotCLI", "jobs");
            }
            return Path.Combine(AppContext.BaseDirectory, "jobs");
        }

        public static async Task<int> ExecuteAsync(CommandLineOptions options, AppConfig config, System.Threading.CancellationToken cancellationToken = default)
        {
            ActiveCancellationToken = cancellationToken;
            ActiveCancellationToken.ThrowIfCancellationRequested();

            // 0. Setup Job Directory
            string timestamp = !string.IsNullOrEmpty(options.ResumeJobId) ? options.ResumeJobId : DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string jobDir = Path.Combine(GetJobsDirectory(), timestamp);

            JobManifestService.InitializeJobDirectory(jobDir, config);

            CurrentJobDirectory = jobDir;

            // Re-initialize logger to write to the logs folder under job directory
            string logsDir = Path.Combine(jobDir, "logs");
            AppLogger.Initialize(config, logsDir);

            // Load or initialize manifest
            string manifestPath = Path.Combine(jobDir, "manifest.json");
            JobManifest currentManifest = JobManifestService.LoadOrInitializeManifest(jobDir, options, config, manifestPath);

            OnPageOcrCompleted = (filePath, pageNum, success, error) =>
            {
                JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, success, error);
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

            // 2. Initialize LM Studio Client and detect models
            string loadedModel = await ModelManagerService.DetectAndCleanVramAsync(options, config, requiresVisionModel);
            string textModel = options.ModelName ?? loadedModel;
            string visionModel = options.VisionModelName ?? loadedModel;

            // Initialize separate clients for OCR, Translation and Review processes
            using var ocrClient = LlmClientFactory.CreateClientForOcr(options, config, config.OcrTimeoutSeconds);
            ocrClient.Temperature = config.OcrTemperature;

            using var translatorClient = LlmClientFactory.CreateClientForTranslation(options, config, config.TranslationTimeoutSeconds);
            translatorClient.Temperature = config.Temperature;

            var ocrService = new OcrService(ocrClient, ocrPrompt, visionModel);
            var translatorService = new TranslatorService(translatorClient, translationPrompt, textModel, options.TargetLanguage);
            translatorService.PreserveFormat = config.PreserveFormat;
            var pageRenderer = new PdfPageRenderer();
            var translatedWriter = new MarkdownWriter();
            var originalWriter = new MarkdownWriter();

            // Initialize Review Service if enabled
            ReviewService? reviewService = null;
            using var reviewClient = (options.Verify && config.ModuleReviewEnabled) ? LlmClientFactory.CreateClientForReview(options, config, config.ReviewTimeoutSeconds) : null;
            if (options.Verify && config.ModuleReviewEnabled && reviewClient != null)
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
                    ActiveCancellationToken.ThrowIfCancellationRequested();
                    string filePath = target.FilePath;
                    string fileName = Path.GetFileName(filePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                    string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");
                    string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

                    // If resuming, copy back files from job directory if they are missing in output directory
                    if (!string.IsNullOrEmpty(options.ResumeJobId))
                    {
                        string outputsDir = Path.Combine(jobDir, "outputs");
                        string jobOutputPath = Path.Combine(outputsDir, Path.GetFileName(outputPath));
                        string jobOriginalOutputPath = Path.Combine(outputsDir, Path.GetFileName(originalOutputPath));
                        
                        // Fallback to job root for legacy jobs
                        if (!File.Exists(jobOutputPath) && File.Exists(Path.Combine(jobDir, Path.GetFileName(outputPath))))
                        {
                            jobOutputPath = Path.Combine(jobDir, Path.GetFileName(outputPath));
                        }
                        if (!File.Exists(jobOriginalOutputPath) && File.Exists(Path.Combine(jobDir, Path.GetFileName(originalOutputPath))))
                        {
                            jobOriginalOutputPath = Path.Combine(jobDir, Path.GetFileName(originalOutputPath));
                        }

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
                        string dataJsonPath = Path.Combine(jobDir, "data", $"{fileNameWithoutExt}_data.json");
                        if (!File.Exists(dataJsonPath) && File.Exists(Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json")))
                        {
                            dataJsonPath = Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json");
                        }
                        
                        if (File.Exists(dataJsonPath))
                        {
                            try
                            {
                                string jsonStr = File.ReadAllText(dataJsonPath);
                                var savedData = System.Text.Json.JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr);
                                if (savedData != null)
                                {
                                    foreach (var data in savedData)
                                    {
                                        cachedStates.Add(new PageProcessState
                                        {
                                            PageNumber = data.PageNumber,
                                            OcrText = data.OriginalText,
                                            TranslatedText = data.TranslatedText,
                                            ReviewedText = data.ReviewedText,
                                            OcrFailed = !data.IsOcrSuccessful,
                                            TranslationFailed = !data.IsTranslationSuccessful,
                                            OcrErrorMessage = data.OcrErrorMessage,
                                            TranslationErrorMessage = data.TranslationErrorMessage
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Warn($"Failed to load existing data from {dataJsonPath}: {ex.Message}");
                            }
                        }

                        List<PageProcessState> pageStates;
                        if (options.Transcribe && config.ModuleExtractionEnabled)
                        {
                            pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer, cachedStates);
                            
                            // Guardar en JSON (solo la extracción original)
                            JobManifestService.SavePageStatesToJson(pageStates, dataJsonPath);
                            
                            int successCount = 0;
                            foreach (var s in pageStates)
                            {
                                if (!s.OcrFailed) successCount++;
                            }
                            AppLogger.InfoConsole($"Extracted {successCount}/{pageStates.Count} pages/chunks successfully.", ConsoleColor.Green);
                            
                            // Mark all extracted pages as completed in the manifest
                            foreach (var s in pageStates)
                            {
                                JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, s.PageNumber, !s.OcrFailed, s.OcrFailed ? s.OcrErrorMessage : null);
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
                        if (LlmClientFactory.IsFatalAuthenticationError(ex))
                        {
                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la extracción: {ex.Message}. Abortando.", ex);
                            throw;
                        }
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
                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, 1, false, ex.Message);
                    }
                }
            }

            // Phase 2: Model Transition
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 2: MODEL TRANSITION                 ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);
            if (requiresVisionModel && textModel != visionModel)
            {
                await ModelManagerService.TransitionModelAsync(options.ApiUrl, config.TranslationTimeoutSeconds, visionModel);
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
                ActiveCancellationToken.ThrowIfCancellationRequested();
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
                    AppLogger.Debug($"Processing pages for {fileName} and updating JSON state.");
                    string dataJsonPath = Path.Combine(jobDir, "data", $"{fileNameWithoutExt}_data.json");
                    if (!File.Exists(dataJsonPath) && File.Exists(Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json")))
                    {
                        dataJsonPath = Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json");
                    }

                    if (options.Translate && config.ModuleTranslationEnabled)
                    {
                        using var translationScope = AppLogger.BeginProcess("Translation");
                        foreach (var state in pageStates)
                        {
                            ActiveCancellationToken.ThrowIfCancellationRequested();
                            int pageNum = state.PageNumber;

                            // Check manifest first if resuming
                            var pageM = fileM?.Pages.Find(p => p.PageNumber == pageNum);
                            if (pageM != null && pageM.TranslationCompleted && !string.IsNullOrEmpty(state.TranslatedText) && !state.TranslationFailed)
                            {
                                AppLogger.Info($"Page {pageNum}: Translation already completed.");
                                continue;
                            }

                            if (!state.TranslationFailed && !string.IsNullOrEmpty(state.TranslatedText))
                            {
                                AppLogger.Info($"Page {pageNum}: Using cached translation from disk.");
                                JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, true, null);
                                continue;
                            }

                            if (state.OcrFailed)
                            {
                                AppLogger.Warn($"Skipping page {pageNum}: OCR/Extraction failed. Propagating error.");
                                state.TranslatedText = state.OcrText;
                                state.TranslationFailed = true;
                                state.TranslationErrorMessage = state.OcrErrorMessage;
                                JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, false, state.OcrErrorMessage);
                            }
                            else
                            {
                                int transMaxRetries = 3;
                                int retries = 0;
                                double currentTemp = config.Temperature;
                                bool success = false;
                                Exception? lastEx = null;

                                while (retries <= transMaxRetries && !success)
                                {
                                    try
                                    {
                                        translatorClient.Temperature = currentTemp;
                                        state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                        state.TranslationFailed = false;
                                        success = true;
                                    }
                                    catch (Exception transEx)
                                    {
                                        if (LlmClientFactory.IsFatalAuthenticationError(transEx))
                                        {
                                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la traducción: {transEx.Message}. Abortando.", transEx);
                                            throw;
                                        }
                                        lastEx = transEx;
                                        // Removed console line break
                                        AppLogger.ErrorConsole($"Translation Error on page/chunk {pageNum}", transEx);
                                        
                                        // If it's a connection issue (socket, timeout), don't increase temp, just fail or break
                                        if (transEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) || 
                                            transEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                                            transEx.Message.Contains("socket", StringComparison.OrdinalIgnoreCase))
                                        {
                                            break;
                                        }

                                        retries++;
                                        currentTemp += 0.1;
                                        if (currentTemp > 0.6) currentTemp = 0.6;

                                        if (retries <= transMaxRetries)
                                        {
                                            AppLogger.Warn($"Translation failed due to non-network error. Retrying with temperature {currentTemp:F1}...");
                                        }
                                    }
                                }

                                if (success)
                                {
                                    JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, true, null);
                                }
                                else
                                {
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = lastEx?.Message ?? "Unknown error";
                                    JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, false, state.TranslationErrorMessage);
                                }
                            }
                            
                            // Guardar estado en JSON después de procesar cada página
                            JobManifestService.SavePageStatesToJson(pageStates, dataJsonPath); 
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
                            ActiveCancellationToken.ThrowIfCancellationRequested();
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
                                        AppLogger.Info($"[RETRY] Page {pageNum}: Extraction successful on retry.");
                                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, true, null);
                                    }
                                    catch (Exception ocrEx)
                                    {
                                        if (LlmClientFactory.IsFatalAuthenticationError(ocrEx))
                                        {
                                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante el reintento de OCR: {ocrEx.Message}. Abortando.", ocrEx);
                                            throw;
                                        }
                                        // Removed console line break
                                        AppLogger.ErrorConsole($"[RETRY] Page {pageNum} OCR extraction retry failed", ocrEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = ocrEx.Message;
                                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, false, ocrEx.Message);
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
                                        AppLogger.Info($"[RETRY] Image OCR retry successful.");
                                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, true, null);
                                    }
                                    catch (Exception imgEx)
                                    {
                                        if (LlmClientFactory.IsFatalAuthenticationError(imgEx))
                                        {
                                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante el reintento de OCR sobre imagen: {imgEx.Message}. Abortando.", imgEx);
                                            throw;
                                        }
                                        // Removed console line break
                                        AppLogger.ErrorConsole($"[RETRY] Image OCR retry failed", imgEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = imgEx.Message;
                                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, false, imgEx.Message);
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
                                            AppLogger.Info($"[RETRY] Page {pageNum}: Re-extraction successful.");
                                            JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, true, null);
                                        }
                                        else
                                        {
                                            JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, false, "Re-extraction returned failed or empty result.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        if (LlmClientFactory.IsFatalAuthenticationError(ex))
                                        {
                                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la re-extracción en reintento: {ex.Message}. Abortando.", ex);
                                            throw;
                                        }
                                        AppLogger.ErrorConsole($"[RETRY] Re-extraction of {fileName} failed", ex);
                                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, false, ex.Message);
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
                                    AppLogger.Info($"[RETRY] Page {pageNum}: Translation successful on retry.");
                                    JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, true, null);
                                }
                                catch (Exception transEx)
                                {
                                    if (LlmClientFactory.IsFatalAuthenticationError(transEx))
                                    {
                                        AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante el reintento de traducción: {transEx.Message}. Abortando.", transEx);
                                        throw;
                                    }
                                    // Removed console line break
                                    AppLogger.ErrorConsole($"[RETRY] Page/Chunk {pageNum} translation retry failed", transEx);
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, false, transEx.Message);
                                }
                            }
                        }
                    }

                    // Phase 4.5: Post-Translation Review (if enabled)
                    if (reviewService != null && config.ModuleReviewEnabled)
                    {
                        using var reviewScope = AppLogger.BeginProcess("Review");
                        AppLogger.InfoConsole($"\n[REVIEW] Running post-translation review for {fileName}...", ConsoleColor.Cyan);

                        foreach (var state in pageStates)
                        {
                            ActiveCancellationToken.ThrowIfCancellationRequested();
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
                                JobManifestService.UpdatePageReview(currentManifest, manifestPath, filePath, state.PageNumber, true, null);
                            }
                            catch (Exception revEx)
                            {
                                if (LlmClientFactory.IsFatalAuthenticationError(revEx))
                                {
                                    AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la revisión: {revEx.Message}. Abortando.", revEx);
                                    throw;
                                }
                                Console.WriteLine(); // fix line break
                                AppLogger.WarnConsole($"[REVIEW] Page {state.PageNumber} review failed (keeping current translation): {revEx.Message}");
                                JobManifestService.UpdatePageReview(currentManifest, manifestPath, filePath, state.PageNumber, false, revEx.Message);
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

                    // Export page states to markdown files before conversion/saving copies
                    var exportPages = pageStates.Select(s => new DocumentPageData
                    {
                        PageNumber = s.PageNumber,
                        OriginalText = s.OcrText,
                        TranslatedText = s.TranslatedText,
                        ReviewedText = s.ReviewedText,
                        IsOcrSuccessful = !s.OcrFailed,
                        IsTranslationSuccessful = !s.TranslationFailed
                    }).ToList();

                    if (options.Transcribe && config.ModuleExtractionEnabled)
                    {
                        MarkdownWriter.ExportToMarkdown(originalOutputPath, fileNameWithoutExt, "Original", exportPages, true);
                    }
                    if (options.Translate && config.ModuleTranslationEnabled)
                    {
                        MarkdownWriter.ExportToMarkdown(outputPath, fileNameWithoutExt, options.TargetLanguage, exportPages, false);
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
                    if (options.GenerateDoc && config.ModuleConversionEnabled && !string.IsNullOrWhiteSpace(options.SelectedFormat))
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
                            JobManifestService.UpdateFileConversion(currentManifest, manifestPath, filePath, true);
                        }
                        catch (Exception convEx)
                        {
                            AppLogger.ErrorConsole($"Conversion Error", convEx);
                            JobManifestService.UpdateFileConversion(currentManifest, manifestPath, filePath, false);
                        }
                    }

                    // Save copies of markdown files to job directory (under outputs/ subdirectory)
                    if (!string.IsNullOrEmpty(CurrentJobDirectory))
                    {
                        try
                        {
                            string outputsDir = Path.Combine(CurrentJobDirectory, "outputs");
                            if (!Directory.Exists(outputsDir))
                            {
                                Directory.CreateDirectory(outputsDir);
                            }

                            if (File.Exists(outputPath))
                            {
                                File.Copy(outputPath, Path.Combine(outputsDir, Path.GetFileName(outputPath)), true);
                            }
                            if (File.Exists(originalOutputPath))
                            {
                                File.Copy(originalOutputPath, Path.Combine(outputsDir, Path.GetFileName(originalOutputPath)), true);
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
            // Error analysis prompt decoupled to caller
 
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

        public static async Task<bool> ReprocessPageAsync(string jobId, string sourceFilePath, int pageNumber, AppConfig config)
        {
            string jobDir = Path.Combine(GetJobsDirectory(), jobId);
            string manifestPath = Path.Combine(jobDir, "manifest.json");
            if (!File.Exists(manifestPath)) return false;

            // 1. Cargar manifiesto
            var options = new CommandLineOptions { ResumeJobId = jobId };
            JobManifest manifest = JobManifestService.LoadOrInitializeManifest(jobDir, options, config, manifestPath);
            
            var fileManifest = manifest.Files.Find(f => f.SourceFilePath.Equals(sourceFilePath, StringComparison.OrdinalIgnoreCase));
            if (fileManifest == null) return false;

            string fileName = Path.GetFileName(sourceFilePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
            string dataJsonPath = Path.Combine(jobDir, "data", $"{fileNameWithoutExt}_data.json");
            if (!File.Exists(dataJsonPath) && File.Exists(Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json")))
            {
                dataJsonPath = Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json");
            }
            if (!File.Exists(dataJsonPath)) return false;

            // 2. Cargar datos del JSON
            List<DocumentPageData> savedData;
            try
            {
                string jsonStr = File.ReadAllText(dataJsonPath);
                savedData = System.Text.Json.JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr) ?? new List<DocumentPageData>();
            }
            catch
            {
                return false;
            }

            var pageData = savedData.Find(d => d.PageNumber == pageNumber);
            if (pageData == null) return false;

            // 3. Inicializar clientes de LLM
            var cmdOptions = new CommandLineOptions
            {
                ApiUrl = config.ApiUrl,
                ModelName = config.DefaultModel,
                VisionModelName = config.DefaultVisionModel,
                TargetLanguage = config.TargetLanguage,
                OutputDirectory = config.OutputDirectory
            };

            string loadedModel = config.DefaultModel ?? "default-model";
            string textModel = config.DefaultModel ?? loadedModel;
            string visionModel = config.DefaultVisionModel ?? loadedModel;

            using var ocrClient = LlmClientFactory.CreateClientForOcr(cmdOptions, config, config.OcrTimeoutSeconds);
            ocrClient.Temperature = config.OcrTemperature;

            using var translatorClient = LlmClientFactory.CreateClientForTranslation(cmdOptions, config, config.TranslationTimeoutSeconds);
            translatorClient.Temperature = config.Temperature;

            var promptLoader = new PromptLoader();
            string ocrPrompt = promptLoader.LoadOcrPrompt();
            string translationPrompt = promptLoader.LoadTranslationPrompt();
            if (!string.IsNullOrWhiteSpace(config.AdditionalPrompt))
            {
                translationPrompt += "\n\nAdditional instructions from user:\n" + config.AdditionalPrompt;
            }

            var ocrService = new OcrService(ocrClient, ocrPrompt, visionModel);
            var translatorService = new TranslatorService(translatorClient, translationPrompt, textModel, config.TargetLanguage);
            var pageRenderer = new PdfPageRenderer();

            string ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            string tempPath = Path.Combine(jobDir, "temp");
            string expectedPngPath = Path.Combine(tempPath, $"{fileNameWithoutExt}_page_{pageNumber}.png");
            
            bool isImage = File.Exists(expectedPngPath) ||
                           ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff";

            if (config.ModuleExtractionEnabled)
            {
                try
                {
                    if (isImage && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] pngBytes = pageRenderer.RenderPageToPng(sourceFilePath, pageNumber);
                        pageData.OriginalText = await ocrService.PerformOcrAsync(pngBytes, pageNumber);
                    }
                    else if (isImage)
                    {
                        byte[] imgBytes = File.ReadAllBytes(sourceFilePath);
                        pageData.OriginalText = await ocrService.PerformOcrAsync(imgBytes, 1);
                    }
                    else
                    {
                        var extractorFactory = new DocumentExtractorFactory();
                        var extractor = extractorFactory.GetExtractor(sourceFilePath);
                        var target = new DocumentTarget { FilePath = sourceFilePath, Mode = "text", PageRange = pageNumber.ToString() };
                        var reExtracted = await extractor.ExtractTextAsync(sourceFilePath, target, ocrService, pageRenderer);
                        var matched = reExtracted.Find(s => s.PageNumber == pageNumber);
                        if (matched != null)
                        {
                            pageData.OriginalText = matched.OcrText;
                        }
                    }
                    pageData.IsOcrSuccessful = true;
                    pageData.OcrErrorMessage = null;
                    JobManifestService.UpdatePageOcr(manifest, manifestPath, sourceFilePath, pageNumber, true, null);
                }
                catch (Exception ex)
                {
                    pageData.IsOcrSuccessful = false;
                    pageData.OcrErrorMessage = ex.Message;
                    JobManifestService.UpdatePageOcr(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 5. Reprocesar Traducción si la extracción tuvo éxito
            if (config.ModuleTranslationEnabled && pageData.IsOcrSuccessful)
            {
                try
                {
                    pageData.TranslatedText = await translatorService.TranslateTextAsync(pageData.OriginalText ?? string.Empty, pageNumber);
                    pageData.IsTranslationSuccessful = true;
                    pageData.TranslationErrorMessage = null;
                    JobManifestService.UpdatePageTranslation(manifest, manifestPath, sourceFilePath, pageNumber, true, null);
                }
                catch (Exception ex)
                {
                    pageData.IsTranslationSuccessful = false;
                    pageData.TranslationErrorMessage = ex.Message;
                    JobManifestService.UpdatePageTranslation(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 6. Reprocesar Revisión si está habilitada y traducción tuvo éxito
            if (config.EnableReview && config.ModuleReviewEnabled && pageData.IsTranslationSuccessful)
            {
                try
                {
                    string reviewPrompt = promptLoader.LoadReviewPrompt();
                    using var reviewClient = LlmClientFactory.CreateClientForReview(cmdOptions, config, config.ReviewTimeoutSeconds);
                    reviewClient.Temperature = config.ReviewTemperature;
                    var reviewService = new ReviewService(reviewClient, reviewPrompt, config.ReviewModel ?? textModel);

                    string reviewed = await reviewService.ReviewTranslationAsync(
                        pageData.OriginalText ?? string.Empty,
                        pageData.TranslatedText ?? string.Empty,
                        pageNumber
                    );
                    pageData.ReviewedText = reviewed;
                    pageData.TranslatedText = reviewed;
                    JobManifestService.UpdatePageReview(manifest, manifestPath, sourceFilePath, pageNumber, true, null);
                }
                catch (Exception ex)
                {
                    JobManifestService.UpdatePageReview(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 7. Guardar cambios en el JSON
            JobManifestService.SavePageStatesToJson(savedData.Select(d => new PageProcessState
            {
                PageNumber = d.PageNumber,
                OcrText = d.OriginalText,
                TranslatedText = d.TranslatedText,
                ReviewedText = d.ReviewedText,
                OcrFailed = !d.IsOcrSuccessful,
                TranslationFailed = !d.IsTranslationSuccessful,
                OcrErrorMessage = d.OcrErrorMessage,
                TranslationErrorMessage = d.TranslationErrorMessage
            }).ToList(), dataJsonPath);

            // 8. Re-exportar a Markdown
            string absoluteOutputDir = Path.GetFullPath(config.OutputDirectory);
            string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{config.TargetLanguage}.md");
            string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

            if (config.ModuleExtractionEnabled)
            {
                MarkdownWriter.ExportToMarkdown(originalOutputPath, fileNameWithoutExt, "Original", savedData, true);
            }
            if (config.ModuleTranslationEnabled)
            {
                MarkdownWriter.ExportToMarkdown(outputPath, fileNameWithoutExt, config.TargetLanguage, savedData, false);
            }

            // Copiar al directorio del trabajo
            string outputsDir = Path.Combine(jobDir, "outputs");
            if (!Directory.Exists(outputsDir)) Directory.CreateDirectory(outputsDir);
            if (File.Exists(outputPath)) File.Copy(outputPath, Path.Combine(outputsDir, Path.GetFileName(outputPath)), true);
            if (File.Exists(originalOutputPath)) File.Copy(originalOutputPath, Path.Combine(outputsDir, Path.GetFileName(originalOutputPath)), true);

            // 9. Re-ejecutar conversión
            if (!string.IsNullOrEmpty(config.DefaultOutputFormat) && config.ModuleConversionEnabled)
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        await OutputFormatConverter.ConvertToFormatsAsync(outputPath, config.DefaultOutputFormat);
                    }
                    if (File.Exists(originalOutputPath))
                    {
                        await OutputFormatConverter.ConvertToFormatsAsync(originalOutputPath, config.DefaultOutputFormat);
                    }
                    JobManifestService.UpdateFileConversion(manifest, manifestPath, sourceFilePath, true);
                }
                catch
                {
                    JobManifestService.UpdateFileConversion(manifest, manifestPath, sourceFilePath, false);
                }
            }

            // Guardar manifiesto
            manifest.Save(manifestPath);
            return true;
        }

    }
}
