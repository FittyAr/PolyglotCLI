using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class ModelManagerService
    {
        public static async Task<string> DetectAndCleanVramAsync(
            CommandLineOptions options, 
            AppConfig config, 
            bool requiresVisionModel)
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
                    loadedModel = await checkClient.GetFirstLoadedModelAsync();
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
                        await checkClient.UnloadAllExceptAsync(textModel);
                    }
                }
                catch (Exception ex)
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
                        string ocrLoaded = await ocrCheckClient.GetFirstLoadedModelAsync();
                        
                        string visionModel = options.VisionModelName ?? ocrLoaded;
                        if (!string.IsNullOrEmpty(visionModel))
                        {
                            AppLogger.Info($"VRAM Management (OCR): Cleaning up loaded models except '{visionModel}'...");
                            await ocrCheckClient.UnloadAllExceptAsync(visionModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to validate/clean VRAM for OCR provider {ocrProviderStr}: {ex.Message}");
                    }
                }
            }

            return loadedModel;
        }

        public static async Task TransitionModelAsync(
            string apiUrl, 
            int timeoutSeconds, 
            string visionModel,
            AppConfig? config = null)
        {
            config ??= AppConfig.Load();
            AppLogger.InfoConsole($"Transitioning models: Unloading OCR Vision model ({visionModel}) to free VRAM...", ConsoleColor.Cyan);
            try
            {
                using var client = LlmClientFactory.CreateClient(config, timeoutSeconds);
                bool unloaded = await client.UnloadModelAsync(visionModel);
                if (unloaded)
                {
                    AppLogger.InfoConsole($"Successfully unloaded model '{visionModel}'.", ConsoleColor.Green);
                }
                else
                {
                    AppLogger.Info($"Model '{visionModel}' was not active or could not be unloaded.");
                }
            }
            catch (Exception unloadEx)
            {
                AppLogger.WarnConsole($"Warning: Failed to unload model '{visionModel}': {unloadEx.Message}");
            }
        }

        public static async Task<(bool Success, string Message)> TestApiConnectionAsync(AppConfig config, int timeoutSeconds = 3)
        {
            try
            {
                using var client = LlmClientFactory.CreateClient(config, timeoutSeconds);
                var models = await client.GetAvailableModelsAsync();
                return (true, $"Connection successful!\nDetected {models.Count} available models for {config.Provider}.");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}
