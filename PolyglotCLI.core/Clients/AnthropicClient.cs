using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class AnthropicClient : ILlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        public double Temperature { get; set; } = 0.3;

        public AnthropicClient(string apiUrl, string? apiKey = null, int timeoutSeconds = 300)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _apiKey = apiKey ?? string.Empty;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            }
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            AppLogger.Debug($"Created AnthropicClient for {_apiUrl} with timeout of {timeoutSeconds}s.");
        }

        public async Task<string> GetFirstLoadedModelAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.Count > 0 ? models[0] : "claude-3-5-sonnet-20241022";
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var models = new List<string>();
            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/models");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idProp))
                            {
                                string id = idProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(id)) models.Add(id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"Anthropic GET /models failed ({ex.Message}).");
            }

            return models;
        }

        public async Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName)
        {
            modelName = string.IsNullOrWhiteSpace(modelName) ? "claude-3-5-sonnet-20241022" : modelName;

            AppLogger.Info($"POST /messages (Anthropic): Sending request to model '{modelName}'");

            var payload = new
            {
                model = modelName,
                system = systemPrompt,
                max_tokens = 8192,
                temperature = Temperature,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.PostAsync($"{_apiUrl}/messages", httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST /messages (Anthropic): Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                string responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Anthropic API error ({response.StatusCode}): {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var contentArray = doc.RootElement.GetProperty("content");
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() == "text")
                    {
                        return item.GetProperty("text").GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /messages (Anthropic) failed.", ex);
                throw;
            }
        }

        public async Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string imageMimeType, string? modelName)
        {
            modelName = string.IsNullOrWhiteSpace(modelName) ? "claude-3-5-sonnet-20241022" : modelName;

            AppLogger.Info($"POST /messages (Anthropic Vision): Sending request to model '{modelName}'");

            string base64Image = Convert.ToBase64String(imageBytes);

            var payload = new
            {
                model = modelName,
                system = systemPrompt,
                max_tokens = 8192,
                temperature = Temperature,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = imageMimeType,
                                    data = base64Image
                                }
                            },
                            new
                            {
                                type = "text",
                                text = userPrompt
                            }
                        }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.PostAsync($"{_apiUrl}/messages", httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST /messages (Anthropic Vision): Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                string responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Anthropic API error ({response.StatusCode}): {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var contentArray = doc.RootElement.GetProperty("content");
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() == "text")
                    {
                        return item.GetProperty("text").GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /messages (Anthropic Vision) failed.", ex);
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
