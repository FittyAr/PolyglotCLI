using System;

namespace PolyglotCLI
{
    public static class LlmClientFactory
    {
        public static ILlmClient CreateClient(AppConfig config, int timeoutSeconds = 300)
        {
            var provider = LlmProviderHelper.ParseProvider(config.Provider);
            string apiUrl = string.IsNullOrWhiteSpace(config.ApiUrl)
                ? LlmProviderHelper.GetDefaultApiUrl(provider)
                : config.ApiUrl;

            string? apiKey = config.GetApiKeyForProvider(config.Provider);

            return CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
        }

        public static ILlmClient CreateClient(CommandLineOptions options, AppConfig config, int timeoutSeconds = 300)
        {
            string providerStr = !string.IsNullOrWhiteSpace(options.Provider) ? options.Provider : config.Provider;
            var provider = LlmProviderHelper.ParseProvider(providerStr);

            string apiUrl = !string.IsNullOrWhiteSpace(options.ApiUrl)
                ? options.ApiUrl
                : (!string.IsNullOrWhiteSpace(config.ApiUrl) ? config.ApiUrl : LlmProviderHelper.GetDefaultApiUrl(provider));

            string? apiKey = !string.IsNullOrWhiteSpace(options.ApiKey) ? options.ApiKey : config.GetApiKeyForProvider(providerStr);

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
                LlmProvider.Anthropic => new AnthropicClient(apiUrl, apiKey, timeoutSeconds),
                LlmProvider.Gemini => new GeminiClient(apiUrl, apiKey, timeoutSeconds),
                _ => new OpenAiCompatibleClient(apiUrl, apiKey, timeoutSeconds)
            };
        }
    }
}
