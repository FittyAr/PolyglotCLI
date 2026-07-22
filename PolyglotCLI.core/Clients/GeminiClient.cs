using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

            AppLogger.Info($"POST /models/{modelName}:generateContent (Gemini): Sending text request");

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = Temperature
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string endpoint = $"{_apiUrl}/models/{modelName}:generateContent";
                var response = await _httpClient.PostAsync(endpoint, httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST Gemini generateContent: Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                string responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Gemini API error ({response.StatusCode}): {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST Gemini generateContent failed.", ex);
                throw;
            }
        }

        public async Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string imageMimeType, string? modelName)
        {
            modelName = string.IsNullOrWhiteSpace(modelName) ? "gemini-1.5-flash" : modelName;
            if (modelName.StartsWith("models/")) modelName = modelName.Substring(7);

            AppLogger.Info($"POST /models/{modelName}:generateContent (Gemini Vision): Sending request");

            string base64Image = Convert.ToBase64String(imageBytes);

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = imageMimeType,
                                    data = base64Image
                                }
                            },
                            new { text = userPrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = Temperature
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string endpoint = $"{_apiUrl}/models/{modelName}:generateContent";
                var response = await _httpClient.PostAsync(endpoint, httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST Gemini generateContent (Vision): Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                string responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Gemini API vision error ({response.StatusCode}): {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST Gemini Vision generateContent failed.", ex);
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
