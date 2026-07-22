using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0010 // Suppress experimental custom endpoint warning

namespace PolyglotCLI
{
    public class OpenAiCompatibleClient : ILlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string? _apiKey;
        public double Temperature { get; set; } = 0.3;

        public OpenAiCompatibleClient(string apiUrl, string? apiKey = null, int timeoutSeconds = 300)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            AppLogger.Debug($"Created OpenAiCompatibleClient for {_apiUrl} with timeout of {timeoutSeconds}s.");
        }

        public async Task<string> GetFirstLoadedModelAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.Count > 0 ? models[0] : string.Empty;
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            AppLogger.Debug($"GET /models: Fetching models list from {_apiUrl}...");
            var models = new List<string>();

            var endpointsToTry = new List<string> { $"{_apiUrl}/models" };
            
            if (_apiUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                string baseAddr = _apiUrl.Substring(0, _apiUrl.Length - 3);
                endpointsToTry.Add($"{baseAddr}/api/tags");
                endpointsToTry.Add($"{baseAddr}/models");
            }

            foreach (var endpoint in endpointsToTry)
            {
                try
                {
                    var response = await _httpClient.GetAsync(endpoint);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);
                        ExtractModelsFromJson(doc.RootElement, models);

                        if (models.Count > 0)
                        {
                            AppLogger.Debug($"GET {endpoint}: Found {models.Count} models dynamically.");
                            break;
                        }
                    }
                    else
                    {
                        string errContent = await response.Content.ReadAsStringAsync();
                        AppLogger.Warn($"GET {endpoint}: Failed (Code: {response.StatusCode}). Content: {errContent}");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"GET {endpoint}: Request failed ({ex.Message}).");
                }
            }

            return models.Distinct().ToList();
        }

        private static void ExtractModelsFromJson(JsonElement element, List<string> models)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    ExtractModelIdFromElement(item, models);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArr.EnumerateArray())
                    {
                        ExtractModelIdFromElement(item, models);
                    }
                }
                else if (element.TryGetProperty("models", out var modelsArr) && modelsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in modelsArr.EnumerateArray())
                    {
                        ExtractModelIdFromElement(item, models);
                    }
                }
            }
        }

        private static void ExtractModelIdFromElement(JsonElement item, List<string> models)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string strVal = item.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(strVal)) models.Add(strVal);
                return;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var propName in new[] { "id", "name", "model", "model_name", "key" })
                {
                    if (item.TryGetProperty(propName, out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    {
                        string id = idProp.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            if (id.StartsWith("models/")) id = id.Substring(7);
                            models.Add(id);
                            break;
                        }
                    }
                }
            }
        }

        public async Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                AppLogger.Debug("SendTextRequestAsync: Model name not specified. Fetching default active model...");
                modelName = await GetFirstLoadedModelAsync();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "default-model";
                }
            }

            AppLogger.Info($"POST /chat/completions (SK): Sending text request to model '{modelName}' (Temp: {Temperature})");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(
                    modelId: modelName,
                    apiKey: _apiKey ?? string.Empty,
                    endpoint: new Uri(_apiUrl),
                    httpClient: _httpClient
                );

                var kernel = builder.Build();
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var history = new ChatHistory();
                history.AddSystemMessage(systemPrompt);
                history.AddUserMessage(userPrompt);

                var settings = new OpenAIPromptExecutionSettings
                {
                    Temperature = Temperature
                };

                var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
                stopwatch.Stop();
                AppLogger.Info($"POST /chat/completions (SK): Finished in {stopwatch.ElapsedMilliseconds}ms.");

                return response.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /chat/completions (SK): Call failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                throw;
            }
        }

        public async Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string imageMimeType, string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                AppLogger.Debug("SendVisionRequestAsync: Model name not specified. Fetching default active model...");
                modelName = await GetFirstLoadedModelAsync();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "default-model";
                }
            }

            AppLogger.Info($"POST /chat/completions (SK Vision): Sending request to model '{modelName}' (Temp: {Temperature})");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(
                    modelId: modelName,
                    apiKey: _apiKey ?? string.Empty,
                    endpoint: new Uri(_apiUrl),
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

                var settings = new OpenAIPromptExecutionSettings
                {
                    Temperature = Temperature
                };

                var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
                stopwatch.Stop();
                AppLogger.Info($"POST /chat/completions (SK Vision): Finished in {stopwatch.ElapsedMilliseconds}ms.");

                return response.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /chat/completions (SK Vision): Call failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                throw;
            }
        }

        public async Task<bool> UnloadModelAsync(string modelIdentifier)
        {
            AppLogger.Info($"VRAM Unload: Requesting unload of model '{modelIdentifier}'...");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string baseAddress = _apiUrl;
                if (baseAddress.EndsWith("/v1"))
                {
                    baseAddress = baseAddress.Substring(0, baseAddress.Length - 3);
                }

                string endpoint = $"{baseAddress}/api/v0/unload";
                var payload = new { model_key = modelIdentifier };
                string jsonPayload = JsonSerializer.Serialize(payload);
                using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, httpContent);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    AppLogger.Info($"VRAM Unload: Successfully unloaded model '{modelIdentifier}'.");
                    return true;
                }

                // Try Ollama unload method (keep_alive: 0)
                string ollamaEndpoint = $"{_apiUrl}/chat/completions";
                var ollamaPayload = new
                {
                    model = modelIdentifier,
                    messages = new object[] { },
                    keep_alive = 0
                };
                using var ollamaContent = new StringContent(JsonSerializer.Serialize(ollamaPayload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(ollamaEndpoint, ollamaContent);
                return true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Debug($"VRAM Unload: Operation failed/ignored ({ex.Message}).");
                return false;
            }
        }

        public async Task UnloadAllExceptAsync(string keepModelIdentifier)
        {
            try
            {
                var models = await GetAvailableModelsAsync();
                foreach (var model in models)
                {
                    if (!model.Equals(keepModelIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        await UnloadModelAsync(model);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"UnloadAllExceptAsync ignored error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
#pragma warning restore SKEXP0010
