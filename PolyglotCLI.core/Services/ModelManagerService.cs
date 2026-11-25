using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class ModelManagerService
    {
        public static async Task<string> DetectAndCleanVramAsync(
            CommandLineOptions options, 
            AppConfig config,
            bool requiresVisionModel,
            CancellationToken cancellationToken = default)
        {
            // Determinar el proveedor principal de traducción
            string transProviderStr = !string.IsNullOrWhiteSpace(options.TranslationProvider) 
                ? options.TranslationProvider 
                : (!string.IsNullOrWhiteSpace(config.TranslationProvider) ? config.TranslationProvider : config.Provider);
            var transProvider = LlmProviderHelper.ParseProvider(transProviderStr);

            string loadedModel = string.Empty;

            // Solo validar/limpiar VRAM si el proveedor soporta descarga de VRAM (es local: Ollama, LmStudio)
            if (LlmProviderHelper.SupportsVramUnload(transProvider))
            {
                AppLogger.Info($"Connecting and validating API server ({transProviderStr}) at: {config.GetProviderConfig(transProviderStr).ApiUrl}");
                try
                {
                    using var checkClient = LlmClientFactory.CreateClientForTranslation(options, config, config.ModelCheckTimeoutSeconds);
                    loadedModel = await checkClient.GetFirstLoadedModelAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(loadedModel))
                    {
                        AppLogger.WarnConsole($"Warning: Could not detect any loaded models in {transProviderStr}.");
                        AppLogger.Info("Please ensure the LLM service is running or configured correctly.");
                    }
                    else
                    {
                        AppLogger.InfoConsole($"Detected model in backend ({transProviderStr}): {loadedModel}", ConsoleColor.Cyan);
                    }

                    string textModel = options.ModelName ?? loadedModel;
                    if (!string.IsNullOrEmpty(textModel))
                    {
                        AppLogger.Info($"VRAM Management: Cleaning up loaded models except '{textModel}'...");
                        await checkClient.UnloadAllExceptAsync(textModel, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AppLogger.Warn($"Failed to validate/clean VRAM for translation provider {transProviderStr}: {ex.Message}");
                }
            }

            // Si se requiere OCR y el proveedor de OCR es local y diferente al de traducción, también limpiamos su VRAM
            if (requiresVisionModel)
            {
                string ocrProviderStr = !string.IsNullOrWhiteSpace(options.OcrProvider)
                    ? options.OcrProvider
                    : (!string.IsNullOrWhiteSpace(config.OcrProvider) ? config.OcrProvider : config.Provider);
                var ocrProvider = LlmProviderHelper.ParseProvider(ocrProviderStr);

                if (LlmProviderHelper.SupportsVramUnload(ocrProvider) && 
                    (!ocrProviderStr.Equals(transProviderStr, StringComparison.OrdinalIgnoreCase)))
                {
                    AppLogger.Info($"Connecting and validating OCR API server ({ocrProviderStr}) at: {config.GetProviderConfig(ocrProviderStr).ApiUrl}");
                    try
                    {
                        using var ocrCheckClient = LlmClientFactory.CreateClientForOcr(options, config, config.ModelCheckTimeoutSeconds);
                        string ocrLoaded = await ocrCheckClient.GetFirstLoadedModelAsync(cancellationToken);
                        
                        string visionModel = options.VisionModelName ?? ocrLoaded;
                        if (!string.IsNullOrEmpty(visionModel))
                        {
                            AppLogger.Info($"VRAM Management (OCR): Cleaning up loaded models except '{visionModel}'...");
                            await ocrCheckClient.UnloadAllExceptAsync(visionModel, cancellationToken);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        AppLogger.Warn($"Failed to validate/clean VRAM for OCR provider {ocrProviderStr}: {ex.Message}");
                    }
                }
            }

            return loadedModel;
        }

        /// <summary>
        /// Descarga el modelo de visión del proveedor de OCR (el mismo
        /// servidor/URL que se usó para el OCR de esta ejecución) para
        /// liberar VRAM antes de traducir. Usa la configuración de la
        /// ejecución recibida, nunca recarga el config desde disco.
        /// </summary>
        public static async Task TransitionModelAsync(
            CommandLineOptions? options,
            AppConfig config,
            int timeoutSeconds,
            string visionModel,
            CancellationToken cancellationToken = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            AppLogger.InfoConsole($"Transitioning models: Unloading OCR Vision model ({visionModel}) to free VRAM...", ConsoleColor.Cyan);
            try
            {
                using var client = LlmClientFactory.CreateClientForOcr(options, config, timeoutSeconds);
                bool unloaded = await client.UnloadModelAsync(visionModel, cancellationToken);
                if (unloaded)
                {
                    AppLogger.InfoConsole($"Successfully unloaded model '{visionModel}'.", ConsoleColor.Green);
                }
                else
                {
                    AppLogger.Info($"Model '{visionModel}' was not active or could not be unloaded.");
                }
            }
            catch (Exception unloadEx) when (unloadEx is not OperationCanceledException)
            {
                AppLogger.WarnConsole($"Warning: Failed to unload model '{visionModel}': {unloadEx.Message}");
            }
        }

        /// <summary>
        /// Prueba la conexión con el proveedor activo. Los clientes LLM
        /// devuelven una lista vacía cuando el servidor no responde (no
        /// lanzan), así que 0 modelos se informa como fallo: sin modelos
        /// la app no puede trabajar de todos modos.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestApiConnectionAsync(AppConfig config, int timeoutSeconds = 3, CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = LlmClientFactory.CreateClient(config, timeoutSeconds);
                var models = await client.GetAvailableModelsAsync(cancellationToken);
                if (models.Count == 0)
                {
                    return (false, $"Connection failed: no se pudo contactar a {config.Provider} o el servidor no expone ningún modelo.");
                }
                return (true, $"Connection successful!\nDetected {models.Count} available models for {config.Provider}.");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}
