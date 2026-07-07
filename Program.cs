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
            using var client = new LmStudioClient(options.ApiUrl);
            
            string loadedModel = await client.GetFirstLoadedModelAsync();
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

            string firstRequiredModel = (options.Mode == "image") ? visionModel : textModel;
            if (!string.IsNullOrEmpty(firstRequiredModel))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[VRAM] Checking loaded models. Ensuring only '{firstRequiredModel}' is loaded for start...");
                Console.ResetColor();
                await client.UnloadAllExceptAsync(firstRequiredModel);
            }

            var ocrService = new OcrService(client, ocrPrompt, visionModel);
            var translatorService = new TranslatorService(client, translationPrompt, textModel, options.TargetLanguage);
            var pageRenderer = new PdfPageRenderer();
            var textExtractor = new PdfTextExtractor();
            var markdownWriter = new MarkdownWriter();

            // Create output directory if it doesn't exist
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
            Console.WriteLine($"Running extraction in {options.Mode.ToUpperInvariant()} mode.");

            var documentStateCache = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<PageProcessState>>();

            foreach (var pdfPath in options.Files)
            {
                string fileName = Path.GetFileName(pdfPath);
                Console.WriteLine($"\nProcessing: {fileName}");
                
                var pageStates = new System.Collections.Generic.List<PageProcessState>();
                try
                {
                    textExtractor.Open(pdfPath);
                    int totalPages = textExtractor.PageCount;
                    Console.WriteLine($"Total pages in document: {totalPages}");

                    // Resolve page list to process
                    System.Collections.Generic.HashSet<int>? pageFilter = null;
                    if (options.Debug)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("DEBUG MODE ACTIVE: Processing only first 2 pages.");
                        Console.ResetColor();
                        pageFilter = new System.Collections.Generic.HashSet<int> { 1 };
                        if (totalPages >= 2)
                        {
                            pageFilter.Add(2);
                        }
                    }
                    else
                    {
                        pageFilter = CommandLineOptions.ParsePageRange(options.PageRange, totalPages);
                    }

                    var resolvedPages = new System.Collections.Generic.List<int>();
                    for (int p = 1; p <= totalPages; p++)
                    {
                        if (pageFilter == null || pageFilter.Contains(p))
                        {
                            resolvedPages.Add(p);
                        }
                    }

                    Console.WriteLine($"Pages to process ({resolvedPages.Count}): {string.Join(", ", resolvedPages)}");

                    // Initialize states
                    foreach (int pageNum in resolvedPages)
                    {
                        pageStates.Add(new PageProcessState { PageNumber = pageNum });
                    }

                    foreach (var state in pageStates)
                    {
                        int pageNum = state.PageNumber;
                        Console.WriteLine($"\n--- [Page {pageNum} of {totalPages}] ---");

                        if (options.Mode == "image")
                        {
                            try
                            {
                                // Render page to image
                                Console.Write("Rendering page to PNG image... ");
                                byte[] pngBytes = pageRenderer.RenderPageToPng(pdfPath, pageNum);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Done.");
                                Console.ResetColor();

                                // Run OCR
                                state.OcrText = await ocrService.PerformOcrAsync(pngBytes, pageNum);
                                state.OcrFailed = false;
                            }
                            catch (Exception ocrEx)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"OCR Error on page {pageNum}: {ocrEx.Message}");
                                Console.ResetColor();
                                state.OcrFailed = true;
                                state.OcrErrorMessage = ocrEx.Message;
                                state.OcrText = $"*Failed to perform OCR on page {pageNum} due to error: {ocrEx.Message}*";
                            }
                        }
                        else
                        {
                            // Text mode
                            try
                            {
                                Console.Write("Extracting text from page... ");
                                state.OcrText = textExtractor.ExtractTextFromPage(pageNum);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Done.");
                                Console.ResetColor();
                                state.OcrFailed = false;

                                if (string.IsNullOrWhiteSpace(state.OcrText))
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Warning: Page contains no selectable text. Try image mode.");
                                    Console.ResetColor();
                                }
                            }
                            catch (Exception extractEx)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Extraction Error on page {pageNum}: {extractEx.Message}");
                                Console.ResetColor();
                                state.OcrFailed = true;
                                state.OcrErrorMessage = extractEx.Message;
                                state.OcrText = $"*Failed to extract text on page {pageNum} due to error: {extractEx.Message}*";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Fatal Error reading file {fileName}: {ex.Message}");
                    Console.ResetColor();
                    pageStates.Add(new PageProcessState 
                    { 
                        PageNumber = 1, 
                        OcrFailed = true, 
                        OcrErrorMessage = ex.Message,
                        OcrText = $"*Failed to read file due to fatal error: {ex.Message}*" 
                    });
                }
                finally
                {
                    textExtractor.Close();
                }

                documentStateCache[pdfPath] = pageStates;
            }

            // Phase 2: Model Transition
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("        PHASE 2: MODEL TRANSITION                 ");
            Console.WriteLine("==================================================");
            if (options.Mode == "image" && textModel != visionModel)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Unloading OCR Vision model ({visionModel}) from VRAM before loading Translation Text model ({textModel})...");
                Console.ResetColor();

                try
                {
                    bool unloaded = await client.UnloadModelAsync(visionModel);
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

            foreach (var pdfPath in options.Files)
            {
                string fileName = Path.GetFileName(pdfPath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(pdfPath);
                string outputPath = Path.Combine(absoluteOutputDir, $"{fileNameWithoutExt}_{options.TargetLanguage}.md");

                Console.WriteLine($"\nTranslating: {fileName}");

                if (!documentStateCache.TryGetValue(pdfPath, out var pageStates) || pageStates.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Skipping translation for {fileName}: No pages were processed.");
                    Console.ResetColor();
                    continue;
                }

                try
                {
                    markdownWriter.Initialize(outputPath, fileName, options.TargetLanguage);

                    foreach (var state in pageStates)
                    {
                        int pageNum = state.PageNumber;

                        // Check if OCR failed
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
                                Console.WriteLine($"Translation Error on page {pageNum}: {transEx.Message}");
                                Console.ResetColor();
                                state.TranslationFailed = true;
                                state.TranslationErrorMessage = transEx.Message;
                                state.TranslatedText = $"*Failed to translate page {pageNum} due to error: {transEx.Message}*";
                            }
                        }

                        // Save translation incrementally page-by-page
                        markdownWriter.AppendPage(pageNum, state.TranslatedText ?? string.Empty);
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
                        Console.WriteLine($"\n[RETRY] Attempt {attempt} of {maxRetries} for {failedPages.Count} failed pages in {fileName}...");
                        Console.ResetColor();

                        // Short delay before retrying
                        await Task.Delay(2000);

                        foreach (var state in failedPages)
                        {
                            int pageNum = state.PageNumber;

                            // 1. Retry OCR if it failed
                            if (state.OcrFailed)
                            {
                                Console.WriteLine($"[RETRY] Page {pageNum}: Retrying OCR...");
                                if (options.Mode == "image")
                                {
                                    try
                                    {
                                        byte[] pngBytes = pageRenderer.RenderPageToPng(pdfPath, pageNum);
                                        state.OcrText = await ocrService.PerformOcrAsync(pngBytes, pageNum);
                                        state.OcrFailed = false;
                                        state.OcrErrorMessage = null;
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
                                else
                                {
                                    try
                                    {
                                        state.OcrText = textExtractor.ExtractTextFromPage(pageNum);
                                        state.OcrFailed = false;
                                        state.OcrErrorMessage = null;
                                    }
                                    catch (Exception extractEx)
                                     {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine($"[RETRY] Page {pageNum} extraction failed again: {extractEx.Message}");
                                        Console.ResetColor();
                                        state.OcrFailed = true;
                                        state.OcrErrorMessage = extractEx.Message;
                                     }
                                }
                            }

                            // 2. Retry translation if OCR is now successful but translation is marked failed
                            if (!state.OcrFailed && state.TranslationFailed)
                            {
                                Console.WriteLine($"[RETRY] Page {pageNum}: Retrying Translation...");
                                try
                                {
                                    state.TranslatedText = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, pageNum);
                                    state.TranslationFailed = false;
                                    state.TranslationErrorMessage = null;

                                    // Update page content in the output file
                                    markdownWriter.UpdatePage(pageNum, state.TranslatedText ?? string.Empty);
                                }
                                catch (Exception transEx)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[RETRY] Page {pageNum} translation failed again: {transEx.Message}");
                                    Console.ResetColor();
                                    state.TranslationFailed = true;
                                    state.TranslationErrorMessage = transEx.Message;
                                    
                                    // Update markdown file to log the latest failure error
                                    markdownWriter.UpdatePage(pageNum, $"*Failed to translate page {pageNum} due to error: {transEx.Message}*");
                                }
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
                        Console.WriteLine($"\nCompleted translating {fileName} with errors in pages: {string.Join(", ", finalFailed)}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\nSuccessfully completed translating {fileName} (all pages succeeded)!");
                        Console.ResetColor();
                    }

                    Console.WriteLine($"Output saved to: {outputPath}");
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
