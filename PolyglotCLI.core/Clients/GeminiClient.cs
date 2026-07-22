using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

#pragma warning disable SKEXP0070 // Suppress experimental Google Gemini connector warning

namespace PolyglotCLI
{
    public class GeminiClient : ILlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        public double Temperature { get; set; } = 0.3;

        public GeminiClient(string apiUrl, string? apiKey = null, int timeoutSeconds = 300)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _apiKey = apiKey ?? string.Empty;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
            }

            AppLogger.Debug($"Created GeminiClient for {_apiUrl} with timeout of {timeoutSeconds}s.");
        }

        public async Task<string> GetFirstLoadedModelAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.Count > 0 ? models[0] : string.Empty;
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var models = new List<string>();
            try
            {
                string url = $"{_apiUrl}/models";
                if (!string.IsNullOrWhiteSpace(_apiKey) && !_httpClient.DefaultRequestHeaders.Contains("x-goog-api-key"))
                {
                    url += $"?key={_apiKey}";
                }

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        foreach (var item in modelsArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString() ?? "";
                                if (name.StartsWith("models/"))
                                {
                                    name = name.Substring(7);
                                }
                                if (!string.IsNullOrEmpty(name))
                                {
                                    models.Add(name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"Gemini GET /models failed ({ex.Message}).");
            }

            return models;
        }

        public async Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName)
        {
            modelName = string.IsNullOrWhiteSpace(modelName) ? "gemini-1.5-flash" : modelName;
            if (modelName.StartsWith("models/")) modelName = modelName.Substring(7);

            AppLogger.Info($"POST /models/{modelName} (SK Gemini): Sending text request");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var builder = Kernel.CreateBuilder();
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: modelName,
                    apiKey: _apiKey,
                    httpClient: _httpClient
                );

                var kernel = builder.Build();
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var history = new ChatHistory();
                history.AddSystemMessage(systemPrompt);
                history.AddUserMessage(userPrompt);

                var settings = new GeminiPromptExecutionSettings
                {
                    Temperature = (float)Temperature
                };

                var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
                stopwatch.Stop();
                AppLogger.Info($"POST Gemini generateContent (SK): Finished in {stopwatch.ElapsedMilliseconds}ms.");

                return response.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST Gemini generateContent (SK) failed.", ex);
                throw;
            }
        }

        public async Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string imageMimeType, string? modelName)
        {
            modelName = string.IsNullOrWhiteSpace(modelName) ? "gemini-1.5-flash" : modelName;
            if (modelName.StartsWith("models/")) modelName = modelName.Substring(7);

            AppLogger.Info($"POST /models/{modelName} (SK Gemini Vision): Sending request");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var builder = Kernel.CreateBuilder();
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: modelName,
                    apiKey: _apiKey,
                    httpClient: _httpClient
                );

                var kernel = builder.Build();
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var history = new ChatHistory();
                history.AddSystemMessage(systemPrompt);

                var userMessage = new ChatMessageContent(AuthorRole.User, new ChatMessageContentItemCollection
                {
                    new TextContent(userPrompt),
                    new ImageContent(new ReadOnlyMemory<byte>(imageBytes), imageMimeType)
                });
                history.Add(userMessage);

                var settings = new GeminiPromptExecutionSettings
                {
                    Temperature = (float)Temperature
                };

                var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
                stopwatch.Stop();
                AppLogger.Info($"POST Gemini generateContent (SK Vision): Finished in {stopwatch.ElapsedMilliseconds}ms.");

                return response.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST Gemini Vision generateContent (SK) failed.", ex);
                throw;
            }
        }

        public Task<bool> UnloadModelAsync(string modelIdentifier) => Task.FromResult(true);

        public Task UnloadAllExceptAsync(string keepModelIdentifier) => Task.CompletedTask;

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
#pragma warning restore SKEXP0070
