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
            string visionModel)
        {
            AppLogger.InfoConsole($"Transitioning models: Unloading OCR Vision model ({visionModel}) to free VRAM...", ConsoleColor.Cyan);
            try
            {
                using var client = new LmStudioClient(apiUrl, timeoutSeconds);
                bool unloaded = await client.UnloadModelAsync(visionModel);
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
    }
}
