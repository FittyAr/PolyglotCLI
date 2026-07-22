using System;

namespace PolyglotCLI
{
    public static class LlmClientFactory
    {
        public static ILlmClient CreateClient(AppConfig config, int timeoutSeconds = 300)
        {
            return CreateClientForStage(config.Provider, null, config, timeoutSeconds);
        }

        public static ILlmClient CreateClient(CommandLineOptions options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = !string.IsNullOrWhiteSpace(options.Provider) ? options.Provider : config.Provider;
            return CreateClientForStage(providerStr, options, config, timeoutSeconds);
        }

        public static ILlmClient CreateClientForOcr(CommandLineOptions? options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = (options != null && !string.IsNullOrWhiteSpace(options.OcrProvider))
                ? options.OcrProvider
                : (!string.IsNullOrWhiteSpace(config.OcrProvider) ? config.OcrProvider : config.Provider);

            return CreateClientForStage(providerStr, options, config, timeoutSeconds);
        }

        public static ILlmClient CreateClientForTranslation(CommandLineOptions? options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = (options != null && !string.IsNullOrWhiteSpace(options.TranslationProvider))
                ? options.TranslationProvider
                : (!string.IsNullOrWhiteSpace(config.TranslationProvider) ? config.TranslationProvider : config.Provider);

            return CreateClientForStage(providerStr, options, config, timeoutSeconds);
        }

        public static ILlmClient CreateClientForReview(CommandLineOptions? options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = (options != null && !string.IsNullOrWhiteSpace(options.ReviewProvider))
                ? options.ReviewProvider
                : (!string.IsNullOrWhiteSpace(config.ReviewProvider) ? config.ReviewProvider : config.Provider);

            return CreateClientForStage(providerStr, options, config, timeoutSeconds);
        }

        public static ILlmClient CreateClientForStage(string? stageProviderStr, CommandLineOptions? options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = !string.IsNullOrWhiteSpace(stageProviderStr) ? stageProviderStr : config.Provider;
            var provider = LlmProviderHelper.ParseProvider(providerStr);

            var pCfg = config.GetProviderConfig(providerStr);
            string apiUrl = (options != null && !string.IsNullOrWhiteSpace(options.ApiUrl) && providerStr.Equals(options.Provider, StringComparison.OrdinalIgnoreCase))
                ? options.ApiUrl
                : (!string.IsNullOrWhiteSpace(pCfg.ApiUrl) ? pCfg.ApiUrl : LlmProviderHelper.GetDefaultApiUrl(provider));

            string? apiKey = (options != null && !string.IsNullOrWhiteSpace(options.ApiKey) && providerStr.Equals(options.Provider, StringComparison.OrdinalIgnoreCase))
                ? options.ApiKey
                : (!string.IsNullOrWhiteSpace(pCfg.ApiKey) ? pCfg.ApiKey : config.GetApiKeyForProvider(providerStr));

            return CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
        }

        public static ILlmClient CreateClient(LlmProvider provider, string apiUrl, string? apiKey = null, int timeoutSeconds = 300)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                apiUrl = LlmProviderHelper.GetDefaultApiUrl(provider);
            }

            return provider switch
            {
                LlmProvider.Gemini => new GeminiClient(apiUrl, apiKey, timeoutSeconds),
                _ => new OpenAiCompatibleClient(apiUrl, apiKey, timeoutSeconds)
            };
        }
    }
}
