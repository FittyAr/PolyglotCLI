using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PolyglotCLI
{
    public static class TranslationOrchestrator
    {
        private const int TranslationMaxRetries = 3;
        private const int FailedPagesMaxRetryRounds = 3;
        private const double RetryTemperatureStep = 0.1;
        private const double RetryTemperatureCeiling = 0.6;
        private static readonly TimeSpan RetryRoundDelay = TimeSpan.FromSeconds(2);

        public static string GetJobsDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                // Estructura {desarrollador}\{programa}: jobs bajo
                // FittyAr\PolyglotCLI\jobs\ en lugar de plantar la
                // app directo en AppData\Roaming.
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "FittyAr", "PolyglotCLI", "jobs");
            }
            return Path.Combine(AppContext.BaseDirectory, "jobs");
        }

        /// <summary>
        /// Copia a <c>{job}\outputs\</c> las salidas de un documento que
        /// están en <paramref name="outputDirectory"/> (la carpeta de salida
        /// real de la ejecución). Ver <see cref="JobOutputFiles"/>.
        /// </summary>
        public static void CopyOutputsToJobDirectory(string jobId, string sourceFilePath, string targetLanguage, string outputDirectory)
        {
            if (!JobManifestService.IsValidJobId(jobId)) return;
            string jobDir = Path.Combine(GetJobsDirectory(), jobId);
            JobOutputFiles.CopyOutputsToJobDirectory(jobDir, outputDirectory, sourceFilePath, targetLanguage);
        }

        public static async Task<int> ExecuteAsync(CommandLineOptions options, AppConfig config, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Copia por-ejecución: los ajustes del job (al reanudar) se
            // aplican sobre esta copia y nunca sobre el singleton de la app.
            AppConfig runConfig = config.Clone();

            // Validación defensiva de las opciones finales (PR 2).
            // Logueamos warnings — NO rechazamos: el usuario quiere
            // ejecutar el job aunque tenga campos "raros".
            AppConfig.ValidateAndLog("TranslationOrchestrator.ExecuteAsync", runConfig);

            if (!string.IsNullOrEmpty(options.ResumeJobId) && !JobManifestService.IsValidJobId(options.ResumeJobId))
            {
                AppLogger.ErrorConsole($"JobId inválido para reanudar: '{options.ResumeJobId}'.");
                return 1;
            }

            // 0. Setup Job Directory
            string timestamp = !string.IsNullOrEmpty(options.ResumeJobId) ? options.ResumeJobId : DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string jobDir = Path.Combine(GetJobsDirectory(), timestamp);

            JobManifestService.InitializeJobDirectory(jobDir, runConfig);

            // Log propio del job (en {job}\logs\) que se cierra al terminar;
            // el logger global de la app no se toca.
            using var jobLogScope = AppLogger.BeginJobLog(runConfig, Path.Combine(jobDir, "logs"));

            // Load or initialize manifest
            string manifestPath = Path.Combine(jobDir, "manifest.json");
            JobManifest currentManifest = JobManifestService.LoadOrInitializeManifest(jobDir, options, runConfig, manifestPath);

            var extractionContext = new ExtractionContext
            {
                JobDirectory = jobDir,
                CancellationToken = cancellationToken,
                OnPageOcrCompleted = (filePath, pageNum, success, error) =>
                    JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, success, error)
            };

            try
            {
                return await RunPipelineAsync(options, runConfig, jobDir, timestamp, currentManifest, manifestPath, extractionContext, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // El usuario canceló el trabajo: NO es un error del
                // pipeline, no debemos marcar el manifest como "Failed".
                // Estado "Cancelled" para que la UI pueda distinguir, y
                // exit code 130 (convención Unix SIGINT).
                try
                {
                    currentManifest.Status = "Cancelled";
                    currentManifest.Save(manifestPath);
                }
                catch (Exception saveEx)
                {
                    AppLogger.Warn($"No se pudo persistir estado 'Cancelled' del manifest: {saveEx.Message}");
                }
                AppLogger.WarnConsole("\n[SYSTEM] Trabajo cancelado por el usuario.");
                return 130;
            }
            catch (Exception pipelineEx)
            {
                currentManifest.Status = "Failed";
                currentManifest.Save(manifestPath);
                AppLogger.ErrorConsole("Pipeline Execution Failed with Exception", pipelineEx);
                return 1;
            }
        }

        private static async Task<int> RunPipelineAsync(
            CommandLineOptions options,
            AppConfig config,
            string jobDir,
            string jobId,
            JobManifest currentManifest,
            string manifestPath,
            ExtractionContext extractionContext,
            CancellationToken cancellationToken)
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
                translationPrompt = BuildTranslationPrompt(promptLoader, options.AdditionalPrompt);
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
                if (target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) || IsImageFile(target.FilePath))
                {
                    requiresVisionModel = true;
                    break;
                }
            }

            // 2. Initialize LLM clients and detect models
            string loadedModel = await ModelManagerService.DetectAndCleanVramAsync(options, config, requiresVisionModel, cancellationToken);
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

            // Initialize Review Service if enabled
            ReviewService? reviewService = null;
            using var reviewClient = (options.Verify && config.ModuleReviewEnabled) ? LlmClientFactory.CreateClientForReview(options, config, config.ReviewTimeoutSeconds) : null;
            if (reviewClient != null)
            {
                try
                {
                    string reviewPrompt = new PromptLoader().LoadReviewPrompt();
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

            // Carpeta de salida: relativa → bajo Documentos\PolyglotCLI;
            // nunca dentro de la instalación (ver AppConfig.GetSafeOutputDirectory).
            string absoluteOutputDir = AppConfig.GetSafeOutputDirectory(options.OutputDirectory);
            if (!Directory.Exists(absoluteOutputDir))
            {
                AppLogger.Info($"Creating output directory: {absoluteOutputDir}");
                Directory.CreateDirectory(absoluteOutputDir);
            }

            // 3. Phase 1: Text Extraction & OCR for all files/pages
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 1: TEXT EXTRACTION & OCR            ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);

            // Estados de trabajo (filtrados por rango) y estados completos
            // a persistir (sin filtrar, para no borrar páginas fuera del
            // rango de data.json). Ambas listas comparten las instancias.
            var documentStateCache = new Dictionary<string, List<PageProcessState>>();
            var persistedStateCache = new Dictionary<string, List<PageProcessState>>();
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
                    cancellationToken.ThrowIfCancellationRequested();
                    string filePath = target.FilePath;
                    string fileName = Path.GetFileName(filePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                    string outputPath = JobOutputFiles.GetTranslatedMarkdownPath(absoluteOutputDir, filePath, options.TargetLanguage);
                    string originalOutputPath = JobOutputFiles.GetOriginalMarkdownPath(absoluteOutputDir, filePath);

                    // If resuming, copy back files from job directory if they are missing in output directory
                    if (!string.IsNullOrEmpty(options.ResumeJobId))
                    {
                        RestorePartialOutputs(jobDir, outputPath, originalOutputPath);
                    }

                    // Check if file is already completed in manifest
                    var fileM = FindFileManifest(currentManifest, filePath);
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

                        string dataJsonPath = ResolveDataJsonPath(jobDir, fileNameWithoutExt);
                        var cachedStates = LoadCachedStates(dataJsonPath);

                        List<PageProcessState> pageStates;
                        if (options.Transcribe && config.ModuleExtractionEnabled)
                        {
                            pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer, extractionContext, cachedStates);

                            // Fusión central del progreso previo (traducciones,
                            // revisiones, razonamiento) para TODOS los formatos:
                            // si el texto de la página/chunk no cambió, se
                            // conserva lo ya traducido en vez de pisarlo.
                            int reused = PageStateMerger.MergeCachedProgress(pageStates, cachedStates);
                            if (reused > 0)
                            {
                                AppLogger.Info($"Reutilizado el progreso previo de {reused} página(s)/chunk(s) de {fileName}.");
                            }

                            JobManifestService.SavePageStatesToJson(pageStates, dataJsonPath);

                            int successCount = 0;
                            int textLength = 0;
                            foreach (var s in pageStates)
                            {
                                if (!s.OcrFailed) successCount++;
                                textLength += s.OcrText?.Trim().Length ?? 0;
                                // Mark all extracted pages as completed in the manifest
                                JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, s.PageNumber, !s.OcrFailed, s.OcrFailed ? s.OcrErrorMessage : null);
                            }
                            AppLogger.InfoConsole($"Extracted {successCount}/{pageStates.Count} pages/chunks successfully.", ConsoleColor.Green);
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

                        persistedStateCache[filePath] = pageStates;
                        documentStateCache[filePath] = FilterByPageRange(pageStates, target.PageRange);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException && !LlmClientFactory.IsFatalAuthenticationError(ex))
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
                        persistedStateCache[filePath] = failedStates;
                        JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, 1, false, ex.Message);
                    }
                    catch (Exception ex) when (LlmClientFactory.IsFatalAuthenticationError(ex))
                    {
                        AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la extracción: {ex.Message}. Abortando.", ex);
                        throw;
                    }
                }
            }

            // Phase 2: Model Transition
            AppLogger.InfoConsole("\n==================================================", ConsoleColor.White);
            AppLogger.InfoConsole("        PHASE 2: MODEL TRANSITION                 ", ConsoleColor.White);
            AppLogger.InfoConsole("==================================================", ConsoleColor.White);
            if (requiresVisionModel && textModel != visionModel)
            {
                await ModelManagerService.TransitionModelAsync(options, config, config.TranslationTimeoutSeconds, visionModel, cancellationToken);
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
                cancellationToken.ThrowIfCancellationRequested();
                string filePath = target.FilePath;
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = JobOutputFiles.GetTranslatedMarkdownPath(absoluteOutputDir, filePath, options.TargetLanguage);
                string originalOutputPath = JobOutputFiles.GetOriginalMarkdownPath(absoluteOutputDir, filePath);

                // Check if file is already completed in manifest
                var fileM = FindFileManifest(currentManifest, filePath);
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
                var statesToPersist = persistedStateCache.TryGetValue(filePath, out var persisted) ? persisted : pageStates;

                try
                {
                    AppLogger.Debug($"Processing pages for {fileName} and updating JSON state.");
                    string dataJsonPath = ResolveDataJsonPath(jobDir, fileNameWithoutExt);

                    if (options.Translate && config.ModuleTranslationEnabled)
                    {
                        using var translationScope = AppLogger.BeginProcess("Translation");
                        foreach (var state in pageStates)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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
                                await TranslatePageWithRetriesAsync(translatorService, translatorClient, config.Temperature, state, cancellationToken);
                                JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, !state.TranslationFailed, state.TranslationFailed ? state.TranslationErrorMessage : null);
                            }

                            // Guardar estado en JSON después de procesar cada página
                            JobManifestService.SavePageStatesToJson(statesToPersist, dataJsonPath);
                        }
                    }
                    else
                    {
                        AppLogger.InfoConsole("Translation skipped.", ConsoleColor.Yellow);
                    }

                    // Phase 4: Retry Loop for Failed Pages of this file
                    await RetryFailedPagesAsync(options, target, pageStates, extractorFactory, ocrService, pageRenderer, translatorService,
                        extractionContext, currentManifest, manifestPath, cancellationToken);

                    // Phase 4.5: Post-Translation Review (if enabled)
                    if (reviewService != null && config.ModuleReviewEnabled)
                    {
                        await ReviewPagesAsync(reviewService, pageStates, fileM, currentManifest, manifestPath, filePath, fileName, cancellationToken);
                    }

                    JobManifestService.SavePageStatesToJson(statesToPersist, dataJsonPath);

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
                        var fileManifest = FindFileManifest(currentManifest, filePath);
                        if (fileManifest != null)
                        {
                            fileManifest.Completed = true;
                            currentManifest.Save(manifestPath);
                        }
                    }

                    // Export page states to markdown files before conversion/saving copies
                    var exportPages = PageStateMapper.ToPageData(pageStates);

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
                        bool converted = await ConvertOutputsAsync(outputPath, originalOutputPath, options.SelectedFormat, cancellationToken);
                        if (!converted)
                        {
                            AppLogger.ErrorConsole($"Conversion Error: no se pudieron generar todos los formatos para {fileName}.");
                        }
                        JobManifestService.UpdateFileConversion(currentManifest, manifestPath, filePath, converted);
                    }

                    // Save copies of all output files to job directory (under outputs/ subdirectory)
                    JobOutputFiles.CopyOutputsToJobDirectory(jobDir, absoluteOutputDir, filePath, options.TargetLanguage);

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
                catch (Exception ex) when (ex is not OperationCanceledException && !LlmClientFactory.IsFatalAuthenticationError(ex))
                {
                    // Error de UN archivo: se registra y se sigue con el
                    // resto. Cancelaciones y errores de autenticación NO
                    // pasan por acá: suben al handler del pipeline.
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

        /// <summary>
        /// Traduce una página reintentando con temperatura creciente ante
        /// errores no-de-red. Deja el resultado (o el error) en
        /// <paramref name="state"/>. Cancelación y errores de autenticación
        /// se propagan.
        /// </summary>
        private static async Task TranslatePageWithRetriesAsync(
            TranslatorService translatorService,
            ILlmClient translatorClient,
            double baseTemperature,
            PageProcessState state,
            CancellationToken cancellationToken)
        {
            int retries = 0;
            double currentTemp = baseTemperature;
            Exception? lastEx = null;

            try
            {
                while (retries <= TranslationMaxRetries)
                {
                    try
                    {
                        translatorClient.Temperature = currentTemp;
                        await TranslatePageAsync(translatorService, state, cancellationToken);
                        return;
                    }
                    catch (Exception transEx) when (transEx is not OperationCanceledException)
                    {
                        if (LlmClientFactory.IsFatalAuthenticationError(transEx))
                        {
                            AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la traducción: {transEx.Message}. Abortando.", transEx);
                            throw;
                        }
                        lastEx = transEx;
                        AppLogger.ErrorConsole($"Translation Error on page/chunk {state.PageNumber}", transEx);

                        // If it's a connection issue (socket, timeout), don't increase temp, just fail
                        if (IsNetworkError(transEx))
                        {
                            break;
                        }

                        retries++;
                        currentTemp = Math.Min(currentTemp + RetryTemperatureStep, RetryTemperatureCeiling);

                        if (retries <= TranslationMaxRetries)
                        {
                            AppLogger.Warn($"Translation failed due to non-network error. Retrying with temperature {currentTemp:F1}...");
                        }
                    }
                }
            }
            finally
            {
                translatorClient.Temperature = baseTemperature;
            }

            state.TranslationFailed = true;
            state.TranslationErrorMessage = lastEx?.Message ?? "Unknown error";
        }

        /// <summary>
        /// Una llamada de traducción: separa el razonamiento &lt;think&gt; y
        /// actualiza el estado de la página.
        /// </summary>
        private static async Task TranslatePageAsync(TranslatorService translatorService, PageProcessState state, CancellationToken cancellationToken)
        {
            string rawTranslation = await translatorService.TranslateTextAsync(state.OcrText ?? string.Empty, state.PageNumber, cancellationToken);
            var (parsedTranslation, thought) = ThinkTagParser.Split(rawTranslation);
            state.TranslatedText = parsedTranslation;
            state.ThoughtText = ThinkTagParser.CombineThoughts(state.ThoughtText, thought);
            state.TranslationFailed = false;
            state.TranslationErrorMessage = null;
        }

        private static async Task RetryFailedPagesAsync(
            CommandLineOptions options,
            DocumentTarget target,
            List<PageProcessState> pageStates,
            DocumentExtractorFactory extractorFactory,
            OcrService ocrService,
            PdfPageRenderer pageRenderer,
            TranslatorService translatorService,
            ExtractionContext extractionContext,
            JobManifest currentManifest,
            string manifestPath,
            CancellationToken cancellationToken)
        {
            string filePath = target.FilePath;
            string fileName = Path.GetFileName(filePath);

            for (int attempt = 1; attempt <= FailedPagesMaxRetryRounds; attempt++)
            {
                var failedPages = pageStates.FindAll(state =>
                    (options.Transcribe && state.OcrFailed) || (options.Translate && state.TranslationFailed));

                if (failedPages.Count == 0)
                {
                    return;
                }

                AppLogger.WarnConsole($"\n[RETRY] Attempt {attempt} of {FailedPagesMaxRetryRounds} for {failedPages.Count} failed pages/chunks in {fileName}...");
                await Task.Delay(RetryRoundDelay, cancellationToken);

                foreach (var state in failedPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int pageNum = state.PageNumber;

                    if (state.OcrFailed)
                    {
                        using var retryOcrScope = AppLogger.BeginProcess("Extraction");
                        AppLogger.Info($"[RETRY] Page/Chunk {pageNum}: Retrying extraction...");
                        try
                        {
                            await ReExtractPageAsync(target, state, extractorFactory, ocrService, pageRenderer, extractionContext, cancellationToken);
                            if (!state.OcrFailed)
                            {
                                AppLogger.Info($"[RETRY] Page {pageNum}: Extraction successful on retry.");
                            }
                            JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, !state.OcrFailed, state.OcrFailed ? state.OcrErrorMessage : null);
                        }
                        catch (Exception ocrEx) when (ocrEx is not OperationCanceledException)
                        {
                            if (LlmClientFactory.IsFatalAuthenticationError(ocrEx))
                            {
                                AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante el reintento de extracción: {ocrEx.Message}. Abortando.", ocrEx);
                                throw;
                            }
                            AppLogger.ErrorConsole($"[RETRY] Page {pageNum} extraction retry failed", ocrEx);
                            state.OcrFailed = true;
                            state.OcrErrorMessage = ocrEx.Message;
                            JobManifestService.UpdatePageOcr(currentManifest, manifestPath, filePath, pageNum, false, ocrEx.Message);
                        }
                    }

                    if (!state.OcrFailed && state.TranslationFailed)
                    {
                        using var retryTransScope = AppLogger.BeginProcess("Translation");
                        AppLogger.Info($"[RETRY] Page/Chunk {pageNum}: Retrying translation...");
                        try
                        {
                            await TranslatePageAsync(translatorService, state, cancellationToken);
                            AppLogger.Info($"[RETRY] Page {pageNum}: Translation successful on retry.");
                            JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, true, null);
                        }
                        catch (Exception transEx) when (transEx is not OperationCanceledException)
                        {
                            if (LlmClientFactory.IsFatalAuthenticationError(transEx))
                            {
                                AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante el reintento de traducción: {transEx.Message}. Abortando.", transEx);
                                throw;
                            }
                            AppLogger.ErrorConsole($"[RETRY] Page/Chunk {pageNum} translation retry failed", transEx);
                            state.TranslationFailed = true;
                            state.TranslationErrorMessage = transEx.Message;
                            JobManifestService.UpdatePageTranslation(currentManifest, manifestPath, filePath, pageNum, false, transEx.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Re-extrae una página fallida: PDF en modo imagen e imágenes van
        /// por OCR directo; el resto re-ejecuta el extractor del formato.
        /// </summary>
        private static async Task ReExtractPageAsync(
            DocumentTarget target,
            PageProcessState state,
            DocumentExtractorFactory extractorFactory,
            OcrService ocrService,
            PdfPageRenderer pageRenderer,
            ExtractionContext extractionContext,
            CancellationToken cancellationToken)
        {
            string filePath = target.FilePath;
            int pageNum = state.PageNumber;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf" && target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                byte[] pngBytes = pageRenderer.RenderPageToPng(filePath, pageNum);
                var ocrRes = await ocrService.PerformOcrAsync(pngBytes, pageNum, cancellationToken);
                ApplyOcrResult(state, ocrRes);
                return;
            }

            if (IsImageFile(filePath))
            {
                byte[] imgBytes = File.ReadAllBytes(filePath);
                var ocrRes = await ocrService.PerformOcrAsync(imgBytes, 1, cancellationToken);
                ApplyOcrResult(state, ocrRes);
                return;
            }

            var extractor = extractorFactory.GetExtractor(filePath);
            var reExtracted = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer, extractionContext);
            var matched = reExtracted.Find(s => s.PageNumber == pageNum);
            if (matched != null && !matched.OcrFailed)
            {
                state.OcrText = matched.OcrText;
                state.OcrFailed = false;
                state.OcrErrorMessage = null;
            }
            else
            {
                state.OcrFailed = true;
                state.OcrErrorMessage = matched?.OcrErrorMessage ?? "Re-extraction returned failed or empty result.";
            }
        }

        private static void ApplyOcrResult(PageProcessState state, OcrResult ocrRes)
        {
            state.OcrText = ocrRes.Text;
            state.ThoughtText = ocrRes.Thought;
            state.OcrFailed = false;
            state.OcrErrorMessage = null;
        }

        private static async Task ReviewPagesAsync(
            ReviewService reviewService,
            List<PageProcessState> pageStates,
            JobFileManifest? fileM,
            JobManifest currentManifest,
            string manifestPath,
            string filePath,
            string fileName,
            CancellationToken cancellationToken)
        {
            using var reviewScope = AppLogger.BeginProcess("Review");
            AppLogger.InfoConsole($"\n[REVIEW] Running post-translation review for {fileName}...", ConsoleColor.Cyan);

            foreach (var state in pageStates)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                        state.PageNumber,
                        cancellationToken
                    );
                    state.ReviewedText = reviewed;
                    state.ReviewFailed = false;
                    state.ReviewErrorMessage = null;

                    state.TranslatedText = reviewed;
                    JobManifestService.UpdatePageReview(currentManifest, manifestPath, filePath, state.PageNumber, true, null);
                }
                catch (Exception revEx) when (revEx is not OperationCanceledException)
                {
                    if (LlmClientFactory.IsFatalAuthenticationError(revEx))
                    {
                        AppLogger.ErrorConsole($"[FATAL] Error crítico de autenticación/API Key durante la revisión: {revEx.Message}. Abortando.", revEx);
                        throw;
                    }
                    AppLogger.WarnConsole($"[REVIEW] Page {state.PageNumber} review failed (keeping current translation): {revEx.Message}");
                    state.ReviewFailed = true;
                    state.ReviewErrorMessage = revEx.Message;
                    JobManifestService.UpdatePageReview(currentManifest, manifestPath, filePath, state.PageNumber, false, revEx.Message);
                }
            }
        }

        /// <summary>
        /// Convierte las salidas Markdown existentes. Devuelve true sólo si
        /// todas las conversiones pedidas se generaron.
        /// </summary>
        private static async Task<bool> ConvertOutputsAsync(string outputPath, string originalOutputPath, string formats, CancellationToken cancellationToken)
        {
            bool converted = true;
            try
            {
                if (File.Exists(outputPath))
                {
                    converted &= await OutputFormatConverter.ConvertToFormatsAsync(outputPath, formats, cancellationToken);
                }
                if (File.Exists(originalOutputPath))
                {
                    converted &= await OutputFormatConverter.ConvertToFormatsAsync(originalOutputPath, formats, cancellationToken);
                }
            }
            catch (Exception convEx) when (convEx is not OperationCanceledException)
            {
                AppLogger.ErrorConsole("Conversion Error", convEx);
                converted = false;
            }
            return converted;
        }

        public static async Task<bool> ReprocessPageAsync(string jobId, string sourceFilePath, int pageNumber, AppConfig config, CancellationToken cancellationToken = default)
        {
            // El jobId llega de la UI / manifest: validarlo antes de armar
            // cualquier ruta (path traversal).
            if (!JobManifestService.IsValidJobId(jobId)) return false;

            string jobDir = Path.Combine(GetJobsDirectory(), jobId);
            string manifestPath = Path.Combine(jobDir, "manifest.json");
            if (!File.Exists(manifestPath)) return false;

            // 1. Cargar manifiesto. Los ajustes del job (idioma, prompt,
            //    modo, carpeta de salida, timeouts...) se aplican sobre una
            //    COPIA por-ejecución, nunca sobre el config compartido.
            AppConfig runConfig = config.Clone();
            var options = new CommandLineOptions { ResumeJobId = jobId };
            JobManifest manifest = JobManifestService.LoadOrInitializeManifest(jobDir, options, runConfig, manifestPath);

            var fileManifest = FindFileManifest(manifest, sourceFilePath);
            if (fileManifest == null) return false;

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
            string dataJsonPath = ResolveDataJsonPath(jobDir, fileNameWithoutExt);
            if (!File.Exists(dataJsonPath)) return false;

            // 2. Cargar datos del JSON
            List<PageProcessState> pageStates;
            try
            {
                string jsonStr = File.ReadAllText(dataJsonPath);
                var savedData = System.Text.Json.JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr) ?? new List<DocumentPageData>();
                pageStates = PageStateMapper.ToPageStates(savedData);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"ReprocessPageAsync: no se pudo leer {dataJsonPath}: {ex.Message}");
                return false;
            }

            var pageState = pageStates.Find(d => d.PageNumber == pageNumber);
            if (pageState == null) return false;

            string targetLanguage = options.TargetLanguage;
            string mode = string.IsNullOrWhiteSpace(options.Mode) ? "text" : options.Mode;
            string textModel = options.ModelName ?? runConfig.DefaultModel ?? string.Empty;
            string visionModel = options.VisionModelName ?? runConfig.DefaultVisionModel ?? textModel;

            // 3. Inicializar clientes de LLM con la configuración del job
            var cmdOptions = new CommandLineOptions
            {
                ApiUrl = runConfig.ApiUrl,
                ModelName = textModel,
                VisionModelName = visionModel,
                TargetLanguage = targetLanguage,
                OutputDirectory = options.OutputDirectory
            };

            using var ocrClient = LlmClientFactory.CreateClientForOcr(cmdOptions, runConfig, runConfig.OcrTimeoutSeconds);
            ocrClient.Temperature = runConfig.OcrTemperature;

            using var translatorClient = LlmClientFactory.CreateClientForTranslation(cmdOptions, runConfig, runConfig.TranslationTimeoutSeconds);
            translatorClient.Temperature = runConfig.Temperature;

            var promptLoader = new PromptLoader();
            string ocrPrompt = promptLoader.LoadOcrPrompt();
            string translationPrompt = BuildTranslationPrompt(promptLoader, options.AdditionalPrompt);

            var ocrService = new OcrService(ocrClient, ocrPrompt, string.IsNullOrEmpty(visionModel) ? null : visionModel);
            var translatorService = new TranslatorService(translatorClient, translationPrompt, string.IsNullOrEmpty(textModel) ? null : textModel, targetLanguage);
            translatorService.PreserveFormat = runConfig.PreserveFormat;
            var pageRenderer = new PdfPageRenderer();

            var target = new DocumentTarget
            {
                FilePath = sourceFilePath,
                Mode = mode,
                PageRange = pageNumber.ToString(),
                MaxCharactersPerChunk = runConfig.MaxCharactersPerChunk,
                ChunkOverlapCharacters = runConfig.ChunkOverlapCharacters
            };
            var extractionContext = new ExtractionContext { JobDirectory = jobDir, CancellationToken = cancellationToken };

            // 4. Re-extraer respetando el modo del job: un PDF en modo
            //    texto se re-extrae como texto (antes siempre hacía OCR).
            if (runConfig.ModuleExtractionEnabled)
            {
                try
                {
                    await ReExtractPageAsync(target, pageState, new DocumentExtractorFactory(), ocrService, pageRenderer, extractionContext, cancellationToken);
                    JobManifestService.UpdatePageOcr(manifest, manifestPath, sourceFilePath, pageNumber, !pageState.OcrFailed, pageState.OcrFailed ? pageState.OcrErrorMessage : null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    pageState.OcrFailed = true;
                    pageState.OcrErrorMessage = ex.Message;
                    JobManifestService.UpdatePageOcr(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 5. Reprocesar Traducción si la extracción tuvo éxito
            if (runConfig.ModuleTranslationEnabled && !pageState.OcrFailed)
            {
                try
                {
                    await TranslatePageAsync(translatorService, pageState, cancellationToken);
                    pageState.ReviewedText = null;
                    JobManifestService.UpdatePageTranslation(manifest, manifestPath, sourceFilePath, pageNumber, true, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    pageState.TranslationFailed = true;
                    pageState.TranslationErrorMessage = ex.Message;
                    JobManifestService.UpdatePageTranslation(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 6. Reprocesar Revisión si el job la usaba y la traducción tuvo éxito
            bool reviewEnabled = (options.Verify || runConfig.EnableReview) && runConfig.ModuleReviewEnabled;
            if (reviewEnabled && !pageState.TranslationFailed && !string.IsNullOrEmpty(pageState.TranslatedText))
            {
                try
                {
                    string reviewPrompt = promptLoader.LoadReviewPrompt();
                    using var reviewClient = LlmClientFactory.CreateClientForReview(cmdOptions, runConfig, runConfig.ReviewTimeoutSeconds);
                    reviewClient.Temperature = runConfig.ReviewTemperature;
                    var reviewService = new ReviewService(reviewClient, reviewPrompt, runConfig.ReviewModel ?? textModel);

                    string reviewed = await reviewService.ReviewTranslationAsync(
                        pageState.OcrText ?? string.Empty,
                        pageState.TranslatedText ?? string.Empty,
                        pageNumber,
                        cancellationToken
                    );
                    pageState.ReviewedText = reviewed;
                    pageState.TranslatedText = reviewed;
                    pageState.ReviewFailed = false;
                    pageState.ReviewErrorMessage = null;
                    JobManifestService.UpdatePageReview(manifest, manifestPath, sourceFilePath, pageNumber, true, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    pageState.ReviewFailed = true;
                    pageState.ReviewErrorMessage = ex.Message;
                    JobManifestService.UpdatePageReview(manifest, manifestPath, sourceFilePath, pageNumber, false, ex.Message);
                }
            }

            // 7. Guardar cambios en el JSON
            JobManifestService.SavePageStatesToJson(pageStates, dataJsonPath);

            // 8. Re-exportar a Markdown en la carpeta de salida del job
            string absoluteOutputDir = AppConfig.GetSafeOutputDirectory(options.OutputDirectory);
            string outputPath = JobOutputFiles.GetTranslatedMarkdownPath(absoluteOutputDir, sourceFilePath, targetLanguage);
            string originalOutputPath = JobOutputFiles.GetOriginalMarkdownPath(absoluteOutputDir, sourceFilePath);
            var exportPages = PageStateMapper.ToPageData(pageStates);

            if (runConfig.ModuleExtractionEnabled)
            {
                MarkdownWriter.ExportToMarkdown(originalOutputPath, fileNameWithoutExt, "Original", exportPages, true);
            }
            if (runConfig.ModuleTranslationEnabled)
            {
                MarkdownWriter.ExportToMarkdown(outputPath, fileNameWithoutExt, targetLanguage, exportPages, false);
            }

            // 9. Re-ejecutar conversión con el formato del job (o el
            //    formato por defecto si el job no pedía uno).
            string? format = options.GenerateDoc && !string.IsNullOrWhiteSpace(options.SelectedFormat)
                ? options.SelectedFormat
                : runConfig.DefaultOutputFormat;
            if (!string.IsNullOrEmpty(format) && runConfig.ModuleConversionEnabled)
            {
                bool converted = await ConvertOutputsAsync(outputPath, originalOutputPath, format, cancellationToken);
                JobManifestService.UpdateFileConversion(manifest, manifestPath, sourceFilePath, converted);
            }

            // Copiar al directorio del trabajo todos los archivos generados
            JobOutputFiles.CopyOutputsToJobDirectory(jobDir, absoluteOutputDir, sourceFilePath, targetLanguage);

            // Guardar manifiesto
            manifest.Save(manifestPath);
            return true;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string BuildTranslationPrompt(PromptLoader promptLoader, string? additionalPrompt)
        {
            string translationPrompt = promptLoader.LoadTranslationPrompt();
            if (!string.IsNullOrWhiteSpace(additionalPrompt))
            {
                translationPrompt += "\n\nAdditional instructions from user:\n" + additionalPrompt;
                AppLogger.Debug($"Appended Additional Prompt ({additionalPrompt.Length} chars).");
            }
            return translationPrompt;
        }

        private static bool IsImageFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff";
        }

        private static bool IsNetworkError(Exception ex)
        {
            return ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("socket", StringComparison.OrdinalIgnoreCase);
        }

        private static JobFileManifest? FindFileManifest(JobManifest manifest, string filePath)
        {
            return manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ruta del data.json de un documento (data/ en jobs actuales; raíz
        /// del job en jobs legacy).
        /// </summary>
        private static string ResolveDataJsonPath(string jobDir, string fileNameWithoutExt)
        {
            string dataJsonPath = Path.Combine(jobDir, "data", $"{fileNameWithoutExt}_data.json");
            string legacyPath = Path.Combine(jobDir, $"{fileNameWithoutExt}_data.json");
            if (!File.Exists(dataJsonPath) && File.Exists(legacyPath))
            {
                return legacyPath;
            }
            return dataJsonPath;
        }

        private static List<PageProcessState> LoadCachedStates(string dataJsonPath)
        {
            if (!File.Exists(dataJsonPath)) return new List<PageProcessState>();
            try
            {
                string jsonStr = File.ReadAllText(dataJsonPath);
                var savedData = System.Text.Json.JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr);
                return savedData != null ? PageStateMapper.ToPageStates(savedData) : new List<PageProcessState>();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to load existing data from {dataJsonPath}: {ex.Message}");
                return new List<PageProcessState>();
            }
        }

        private static List<PageProcessState> FilterByPageRange(List<PageProcessState> pageStates, string? pageRange)
        {
            if (string.IsNullOrWhiteSpace(pageRange) || pageRange.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return pageStates;
            }

            int maxPage = 0;
            foreach (var state in pageStates)
            {
                if (state.PageNumber > maxPage) maxPage = state.PageNumber;
            }
            var pageFilter = CommandLineOptions.ParsePageRange(pageRange, maxPage);
            if (pageFilter == null) return pageStates;

            var filtered = pageStates.FindAll(s => pageFilter.Contains(s.PageNumber));
            AppLogger.Info($"Filtered page states to range '{pageRange}'. Active pages: {string.Join(", ", filtered.ConvertAll(s => s.PageNumber))}");
            return filtered;
        }

        /// <summary>
        /// Al reanudar, restaura desde {job}\outputs\ (o la raíz del job en
        /// jobs legacy) los Markdown parciales que falten en la carpeta de
        /// salida.
        /// </summary>
        private static void RestorePartialOutputs(string jobDir, string outputPath, string originalOutputPath)
        {
            string outputsDir = Path.Combine(jobDir, "outputs");
            foreach (var target in new[] { outputPath, originalOutputPath })
            {
                string name = Path.GetFileName(target);
                string candidate = Path.Combine(outputsDir, name);
                if (!File.Exists(candidate) && File.Exists(Path.Combine(jobDir, name)))
                {
                    candidate = Path.Combine(jobDir, name);
                }
                if (!File.Exists(target) && File.Exists(candidate))
                {
                    File.Copy(candidate, target, true);
                    AppLogger.Info($"Restored partial output file from job directory: {target}");
                }
            }
        }
    }
}
