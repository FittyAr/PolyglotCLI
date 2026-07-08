using System;
using System.IO;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine("                  PolyglotCLI                     ");
            Console.WriteLine("==================================================");

            // Load Configuration
            var config = AppConfig.Load();

            // 1. Parse command line options or launch interactive menu
            CommandLineOptions? options;
            if (args.Length == 0)
            {
                options = await InteractiveMenu.RunAsync(config);
                if (options == null)
                {
                    return 0; // Cancelled by user
                }
            }
            else
            {
                options = CommandLineOptions.Parse(args, config);
                if (options == null)
                {
                    return 1;
                }
            }

            // 2. Load Prompts
            string ocrPrompt;
            string translationPrompt;
            try
            {
                var promptLoader = new PromptLoader();
                ocrPrompt = promptLoader.LoadOcrPrompt();
                translationPrompt = promptLoader.LoadTranslationPrompt();
                if (!string.IsNullOrWhiteSpace(options.AdditionalPrompt))
                {
                    translationPrompt += "\n\nAdditional instructions from user:\n" + options.AdditionalPrompt;
                }
                Console.WriteLine("Loaded prompts successfully.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            // 3. Initialize LM Studio Client and detect models
            Console.WriteLine($"Connecting to LM Studio API at: {options.ApiUrl} ...");
            using var checkClient = new LmStudioClient(options.ApiUrl, config.ModelCheckTimeoutSeconds);
            
            string loadedModel = await checkClient.GetFirstLoadedModelAsync();
            if (string.IsNullOrWhiteSpace(loadedModel))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: Could not detect any loaded models in LM Studio.");
                Console.WriteLine("Please ensure LM Studio is running, local server is started, and a model is loaded.");
                Console.WriteLine("Continuing request, relying on LM Studio API default model routing.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Detected loaded model: {loadedModel}");
                Console.ResetColor();
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[VRAM] Checking loaded models. Ensuring only '{firstRequiredModel}' is loaded for start...");
                Console.ResetColor();
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
            using var reviewClient = config.EnableReview ? new LmStudioClient(options.ApiUrl, config.ReviewTimeoutSeconds) : null;
            if (config.EnableReview && reviewClient != null)
            {
                try
                {
                    var promptLoader2 = new PromptLoader();
                    string reviewPrompt = promptLoader2.LoadReviewPrompt();
                    string reviewModel = config.ReviewModel ?? textModel;
                    
                    reviewClient.Temperature = config.ReviewTemperature;
                    reviewService = new ReviewService(reviewClient, reviewPrompt, reviewModel);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"Post-translation review enabled (model: {reviewModel}).");
                    Console.ResetColor();
                }
                catch (Exception revEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not load review prompt, review disabled: {revEx.Message}");
                    Console.ResetColor();
                }
            }

            // Create output directory if it doesn't exist
            string absoluteOutputDir = Path.GetFullPath(options.OutputDirectory);
            if (!Directory.Exists(absoluteOutputDir))
            {
                Directory.CreateDirectory(absoluteOutputDir);
            }

            // 4. Phase 1: Text Extraction & OCR for all files/pages
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("        PHASE 1: TEXT EXTRACTION & OCR            ");
            Console.WriteLine("==================================================");
            
            if (options.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("DEBUG MODE ACTIVE: Limiting processing to first 2 pages/chunks.");
                Console.ResetColor();
                foreach (var t in options.DocumentTargets)
                {
                    t.PageRange = "1-2";
                }
            }

            var documentStateCache = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<PageProcessState>>();
            var extractorFactory = new DocumentExtractorFactory();

            foreach (var target in options.DocumentTargets)
            {
                string filePath = target.FilePath;
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\nProcessing: {fileName} (Mode: {target.Mode.ToUpperInvariant()})");
                
                try
                {
                    var extractor = extractorFactory.GetExtractor(filePath);
                    var pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer);
                    
                    int successCount = 0;
                    foreach (var s in pageStates)
                    {
                        if (!s.OcrFailed) successCount++;
                    }
                    Console.WriteLine($"Extracted {successCount}/{pageStates.Count} pages/chunks successfully.");
                    
                    int textLength = 0;
                    foreach (var s in pageStates)
                    {
                        textLength += s.OcrText?.Trim().Length ?? 0;
                    }
                    if (textLength == 0 && target.Mode.Equals("text", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING] No selectable text was found in '{fileName}'.");
                        Console.WriteLine($"          If this is a scanned document (image-only PDF), please run again with OCR mode set to 'Image'.");
                        Console.WriteLine($"          In the interactive menu, select the file and press [T] or [M] to toggle OCR mode.");
                        Console.ResetColor();
                    }
                    
                    documentStateCache[filePath] = pageStates;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Fatal Error reading file {fileName}: {ex.Message}");
                    Console.ResetColor();
                    
                    var failedStates = new System.Collections.Generic.List<PageProcessState>
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
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("        PHASE 2: MODEL TRANSITION                 ");
            Console.WriteLine("==================================================");
            if (requiresVisionModel && textModel != visionModel)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Unloading OCR Vision model ({visionModel}) from VRAM before loading Translation Text model ({textModel})...");
                Console.ResetColor();

                try
                {
                    bool unloaded = await translatorClient.UnloadModelAsync(visionModel);
                    if (unloaded)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Successfully unloaded model '{visionModel}'.");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"Model '{visionModel}' was not active or could not be found.");
                    }
                }
                catch (Exception unloadEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Failed to unload model '{visionModel}': {unloadEx.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine($"Model '{textModel}' is loaded for translation.");
            }

            // Phase 3: Translation & Markdown Saving
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("        PHASE 3: TRANSLATION & SAVING             ");
            Console.WriteLine("==================================================");

            foreach (var target in options.DocumentTargets)
            {
                string filePath = target.FilePath;
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");
                string originalOutputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_original.md");

                Console.WriteLine($"\nTranslating: {fileName}");

                if (!documentStateCache.TryGetValue(filePath, out var pageStates) || pageStates.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Skipping translation for {fileName}: No content was extracted.");
                    Console.ResetColor();
                    continue;
                }

                try
                {
                    translatedWriter.Initialize(outputPath, fileName, options.TargetLanguage);
                    originalWriter.Initialize(originalOutputPath, fileName, "Original");

                    foreach (var state in pageStates)
                    {
                        int pageNum = state.PageNumber;

                        // Check if OCR/Extraction failed
                        if (state.OcrFailed)
                        {
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
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Translation Error on page/chunk {pageNum}: {transEx.Message}");
                                Console.ResetColor();
                                state.TranslationFailed = true;
                                state.TranslationErrorMessage = transEx.Message;
                                state.TranslatedText = $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*";
                            }
                        }

                        // Save original text and translation incrementally page-by-page/chunk-by-chunk
                        originalWriter.AppendPage(pageNum, state.OcrText ?? string.Empty);
                        translatedWriter.AppendPage(pageNum, state.TranslatedText ?? string.Empty);
                    }

                    // Phase 4: Retry Loop for Failed Pages of this file
                    int maxRetries = 3;
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        var failedPages = new System.Collections.Generic.List<PageProcessState>();
                        foreach (var state in pageStates)
                        {
                            if (state.OcrFailed || state.TranslationFailed)
                            {
                                failedPages.Add(state);
                            }
                        }

                        if (failedPages.Count == 0)
                        {
                            break; // No failed pages, exit retry loop
                        }

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\n[RETRY] Attempt {attempt} of {maxRetries} for {failedPages.Count} failed pages/chunks in {fileName}...");
                        Console.ResetColor();

                        // Short delay before retrying
                        await Task.Delay(2000);

                        foreach (var state in failedPages)
                        {
                            int pageNum = state.PageNumber;

                            // 1. Retry OCR/Extraction if it failed
                            if (state.OcrFailed)
                            {
                                Console.WriteLine($"[RETRY] Page/Chunk {pageNum}: Retrying extraction...");
                                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                                if (ext == ".pdf" && target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        byte[] pngBytes = pageRenderer.RenderPageToPng(filePath, pageNum);
                                        state.OcrText = await ocrService.PerformOcrAsync(pngBytes, pageNum);
                                        state.OcrFailed = false;
                                        state.OcrErrorMessage = null;
                                        originalWriter.UpdatePage(pageNum, state.OcrText ?? string.Empty);
                                    }
                                    catch (Exception ocrEx)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine($"[RETRY] Page {pageNum} OCR failed again: {ocrEx.Message}");
                                        Console.ResetColor();
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
                                        originalWriter.UpdatePage(pageNum, state.OcrText ?? string.Empty);
                                    }
                                    catch (Exception imgEx)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine($"[RETRY] Image OCR failed again: {imgEx.Message}");
                                        Console.ResetColor();
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
                                            originalWriter.UpdatePage(pageNum, state.OcrText ?? string.Empty);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine($"[RETRY] Re-extraction of {fileName} failed: {ex.Message}");
                                        Console.ResetColor();
                                    }
                                }
                            }

                            // 2. Retry translation if extraction is now successful but translation is marked failed
                            if (!state.OcrFailed && state.TranslationFailed)
                            {
                                Console.WriteLine($"[RETRY] Page/Chunk {pageNum}: Retrying Translation...");
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                    state.TranslationErrorMessage = null;

                                    // Update page content in the output file
                                    translatedWriter.UpdatePage(pageNum, state.TranslatedText ?? string.Empty);
                                }
                                catch (Exception transEx)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[RETRY] Page/Chunk {pageNum} translation failed again: {transEx.Message}");
                                    Console.ResetColor();
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    
                                    // Update markdown file to log the latest failure error
                                    translatedWriter.UpdatePage(pageNum, $"*Failed to translate page/chunk {pageNum} due to error: {transEx.Message}*");
                                }
                            }
                        }
                    }

                    // Phase 4.5: Post-Translation Review (if enabled)
                    if (reviewService != null)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[REVIEW] Running post-translation review for {fileName}...");
                        Console.ResetColor();

                        foreach (var state in pageStates)
                        {
                            if (state.OcrFailed || state.TranslationFailed)
                            {
                                continue; // Skip failed pages
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

                                // Update translated text with reviewed version
                                state.TranslatedText = reviewed;
                                translatedWriter.UpdatePage(state.PageNumber, reviewed);
                            }
                            catch (Exception revEx)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[REVIEW] Page {state.PageNumber} review failed (keeping original translation): {revEx.Message}");
                                Console.ResetColor();
                                state.ReviewFailed = true;
                                state.ReviewErrorMessage = revEx.Message;
                            }
                        }
                    }

                    // Print final status for this file
                    var finalFailed = new System.Collections.Generic.List<int>();
                    foreach (var state in pageStates)
                    {
                        if (state.OcrFailed || state.TranslationFailed)
                        {
                            finalFailed.Add(state.PageNumber);
                        }
                    }

                    if (finalFailed.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nCompleted translating {fileName} with errors in pages/chunks: {string.Join(", ", finalFailed)}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\nSuccessfully completed translating {fileName} (all pages/chunks succeeded)!");
                        Console.ResetColor();
                    }

                    Console.WriteLine($"Original text saved to: {originalOutputPath}");
                    Console.WriteLine($"Output saved to: {outputPath}");

                    // Phase 5: Output Format Conversion
                    if (!string.IsNullOrWhiteSpace(config.OutputFormats) && !config.OutputFormats.Trim().Equals("md", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Converting output to additional formats...");
                        OutputFormatConverter.ConvertToFormats(outputPath, config.OutputFormats);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Fatal Error during translation/saving for {fileName}: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("           Translation Process Finished           ");
            Console.WriteLine("==================================================");
            return 0;
        }
    }
}
