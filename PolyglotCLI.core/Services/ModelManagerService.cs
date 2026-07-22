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
            AppLogger.Info($"Connecting and validating API server ({options.Provider}) at: {options.ApiUrl}");
            using var checkClient = LlmClientFactory.CreateClient(options, config, config.ModelCheckTimeoutSeconds);
            
            string loadedModel = await checkClient.GetFirstLoadedModelAsync();
            if (string.IsNullOrWhiteSpace(loadedModel))
            {
                AppLogger.WarnConsole($"Warning: Could not detect any loaded models in {options.Provider}.");
                AppLogger.Info("Please ensure the LLM service is running or configured correctly.");
            }
            else
            {
                AppLogger.InfoConsole($"Detected model in backend ({options.Provider}): {loadedModel}", ConsoleColor.Cyan);
            }

            string textModel = options.ModelName ?? loadedModel;
            string visionModel = options.VisionModelName ?? loadedModel;

            string firstRequiredModel = requiresVisionModel ? visionModel : textModel;
            if (!string.IsNullOrEmpty(firstRequiredModel))
            {
                AppLogger.Info($"VRAM Management: Cleaning up loaded models except '{firstRequiredModel}'...");
                await checkClient.UnloadAllExceptAsync(firstRequiredModel);
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
