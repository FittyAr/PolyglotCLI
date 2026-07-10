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
        public static async Task<int> ExecuteAsync(CommandLineOptions options, AppConfig config)
        {
            var totalPipelineStopwatch = Stopwatch.StartNew();
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
            
            if (options.Debug)
            {
                AppLogger.WarnConsole("DEBUG MODE ACTIVE: Limiting processing to first 2 pages/chunks.");
                foreach (var t in options.DocumentTargets)
                {
                    t.PageRange = "1-2";
                }
            }

            var documentStateCache = new Dictionary<string, List<PageProcessState>>();
            var extractorFactory = new DocumentExtractorFactory();

            foreach (var target in options.DocumentTargets)
            {
                string filePath = target.FilePath;
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");
                string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

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
                        foreach (var state in pageStates)
                        {
                            int pageNum = state.PageNumber;

                            if (!state.TranslationFailed && !string.IsNullOrEmpty(state.TranslatedText))
                            {
                                AppLogger.Info($"Page {pageNum}: Using cached translation from disk.");
                                translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText);
                                continue;
                            }

                            if (state.OcrFailed)
                            {
                                AppLogger.Warn($"Skipping page {pageNum}: OCR/Extraction failed. Propagating error.");
                                state.TranslatedText = state.OcrText;
                                state.TranslationFailed = true;
                                state.TranslationErrorMessage = state.OcrErrorMessage;
                            }
                            else
                            {
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                }
                                catch (Exception transEx)
                                {
                                    // Fix layout: print a newline first to separate from the unfinished Console.Write inside TranslatorService
                                    Console.WriteLine();
                                    AppLogger.ErrorConsole($"Translation Error on page/chunk {pageNum}", transEx);
                                    
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    state.TranslatedText = $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*";
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
                                    }
                                    catch (Exception ocrEx)
                                    {
                                        Console.WriteLine(); // fix line break
                                        AppLogger.ErrorConsole($"[RETRY] Page {pageNum} OCR extraction retry failed", ocrEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = ocrEx.Message;
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
                                    }
                                    catch (Exception imgEx)
                                    {
                                        Console.WriteLine(); // fix line break
                                        AppLogger.ErrorConsole($"[RETRY] Image OCR retry failed", imgEx);
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = imgEx.Message;
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
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AppLogger.ErrorConsole($"[RETRY] Re-extraction of {fileName} failed", ex);
                                    }
                                }
                            }

                            if (!state.OcrFailed && state.TranslationFailed)
                            {
                                AppLogger.Info($"[RETRY] Page/Chunk {pageNum}: Retrying translation...");
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                    state.TranslationErrorMessage = null;
                                    translatedWriter.SaveOrUpdatePage(pageNum, state.TranslatedText ?? string.Empty);
                                    AppLogger.Info($"[RETRY] Page {pageNum}: Translation successful on retry.");
                                }
                                catch (Exception transEx)
                                {
                                    Console.WriteLine(); // fix line break
                                    AppLogger.ErrorConsole($"[RETRY] Page/Chunk {pageNum} translation retry failed", transEx);
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    translatedWriter.SaveOrUpdatePage(pageNum, $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*");
                                }
                            }
                        }
                    }

                    // Phase 4.5: Post-Translation Review (if enabled)
                    if (reviewService != null)
                    {
                        AppLogger.InfoConsole($"\n[REVIEW] Running post-translation review for {fileName}...", ConsoleColor.Cyan);

                        foreach (var state in pageStates)
                        {
                            if (state.OcrFailed || state.TranslationFailed)
                            {
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
                            }
                            catch (Exception revEx)
                            {
                                Console.WriteLine(); // fix line break
                                AppLogger.WarnConsole($"[REVIEW] Page {state.PageNumber} review failed (keeping current translation): {revEx.Message}");
                            }
                        }
                    }

                    // Print final status for this file
                    var finalFailed = new List<int>();
                    foreach (var state in pageStates)
                    {
                        if (state.OcrFailed || state.TranslationFailed)
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
                        AppLogger.InfoConsole($"Converting output to {options.SelectedFormat.ToUpperInvariant()}...", ConsoleColor.Cyan);
                        if (File.Exists(outputPath))
                        {
                            OutputFormatConverter.ConvertToFormats(outputPath, options.SelectedFormat);
                        }
                        if (File.Exists(originalOutputPath))
                        {
                            OutputFormatConverter.ConvertToFormats(originalOutputPath, options.SelectedFormat);
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

            totalPipelineStopwatch.Stop();
            AppLogger.Info("==================================================");
            AppLogger.Info($"Translation Process Finished in {totalPipelineStopwatch.Elapsed.TotalSeconds:F2} seconds.");
            AppLogger.Info("==================================================");
            return 0;
        }
    }
}
