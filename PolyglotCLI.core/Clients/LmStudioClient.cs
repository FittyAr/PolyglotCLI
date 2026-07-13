using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class LmStudioClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        public double Temperature { get; set; } = 0.3;

        public LmStudioClient(string apiUrl, int timeoutSeconds = 300)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            AppLogger.Debug($"Created LmStudioClient for {_apiUrl} with timeout of {timeoutSeconds}s.");
        }

        public async Task<string> GetFirstLoadedModelAsync()
        {
            AppLogger.Debug("GET /models: Fetching loaded models list...");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/models");
                stopwatch.Stop();
                AppLogger.Debug($"GET /models: Response received in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var dataElement = doc.RootElement.GetProperty("data");
                    if (dataElement.GetArrayLength() > 0)
                    {
                        string firstModel = dataElement[0].GetProperty("id").GetString() ?? string.Empty;
                        AppLogger.Debug($"GET /models: Found loaded model: '{firstModel}'");
                        return firstModel;
                    }
                    AppLogger.Debug("GET /models: No models currently loaded in LM Studio.");
                }
                else
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    AppLogger.Warn($"GET /models: Failed (Code: {response.StatusCode}). Content: {errContent}");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"GET /models: Connection request failed in {stopwatch.ElapsedMilliseconds}ms.", ex);
            }

            return string.Empty;
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            AppLogger.Debug("GET /models: Fetching loaded models list...");
            var models = new List<string>();
            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/models");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var dataElement = doc.RootElement.GetProperty("data");
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        string id = item.GetProperty("id").GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(id))
                        {
                            models.Add(id);
                        }
                    }
                }
                else
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    AppLogger.Warn($"GET /models: Failed (Code: {response.StatusCode}). Content: {errContent}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("GET /models: Connection request failed.", ex);
                throw;
            }
            return models;
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

            AppLogger.Info($"POST /chat/completions: Sending text request to model '{modelName}' (Temp: {Temperature})");
            AppLogger.Debug($"System prompt length: {systemPrompt.Length} chars, User prompt length: {userPrompt.Length} chars");

            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = Temperature
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST /chat/completions: Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    var exception = new HttpRequestException($"LM Studio request failed with status code {response.StatusCode}. Details: {errorContent}");
                    AppLogger.Error($"POST /chat/completions: Request failed.", exception);
                    throw exception;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                
                string contentResult = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                AppLogger.Debug($"POST /chat/completions: Successful. Response text length: {contentResult.Length} chars.");
                return contentResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /chat/completions: Call failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
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

            AppLogger.Info($"POST /chat/completions (Vision): Sending request to model '{modelName}' (Temp: {Temperature})");
            AppLogger.Debug($"Image size: {imageBytes.Length / 1024.0:F2} KB, Mime: {imageMimeType}");

            string base64Image = Convert.ToBase64String(imageBytes);
            var imageUrl = $"data:{imageMimeType};base64,{base64Image}";

            var payload = new
            {
                model = modelName,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = userPrompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = imageUrl }
                            }
                        }
                    }
                },
                temperature = Temperature
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", httpContent);
                stopwatch.Stop();
                AppLogger.Info($"POST /chat/completions (Vision): Finished in {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    var exception = new HttpRequestException($"LM Studio vision request failed with status code {response.StatusCode}. Details: {errorContent}");
                    AppLogger.Error($"POST /chat/completions (Vision): Vision request failed.", exception);
                    throw exception;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                string contentResult = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                AppLogger.Debug($"POST /chat/completions (Vision): Successful. Response text length: {contentResult.Length} chars.");
                return contentResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"POST /chat/completions (Vision): Call failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
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

                var response = await _httpClient.GetAsync($"{baseAddress}/api/v1/models");
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.Warn($"VRAM Unload: Failed to fetch active models (Status: {response.StatusCode}).");
                    return false;
                }

                string content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (!doc.RootElement.TryGetProperty("models", out var modelsElement))
                {
                    AppLogger.Warn("VRAM Unload: No active models node found in response.");
                    return false;
                }

                bool anyUnloaded = false;
                foreach (var modelItem in modelsElement.EnumerateArray())
                {
                    string? key = modelItem.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                    if (string.Equals(key, modelIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        if (modelItem.TryGetProperty("loaded_instances", out var instancesElement) && 
                            instancesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var instance in instancesElement.EnumerateArray())
                            {
                                string? instanceId = instance.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                                if (!string.IsNullOrEmpty(instanceId))
                                {
                                    AppLogger.Info($"VRAM Unload: Unloading model instance '{instanceId}'...");
                                    var payload = new { instance_id = instanceId };
                                    string jsonPayload = JsonSerializer.Serialize(payload);
                                    using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                                    var unloadResponse = await _httpClient.PostAsync($"{baseAddress}/api/v1/models/unload", httpContent);
                                    if (unloadResponse.IsSuccessStatusCode)
                                    {
                                        AppLogger.Info($"VRAM Unload: Successfully unloaded '{instanceId}'.");
                                        anyUnloaded = true;
                                    }
                                    else
                                    {
                                        string err = await unloadResponse.Content.ReadAsStringAsync();
                                        AppLogger.Warn($"VRAM Unload: Unload call failed for '{instanceId}'. Details: {err}");
                                    }
                                }
                            }
                        }
                    }
                }

                stopwatch.Stop();
                AppLogger.Debug($"VRAM Unload: Finished in {stopwatch.ElapsedMilliseconds}ms. Unloaded: {anyUnloaded}");
                return anyUnloaded;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"VRAM Unload: Failed to unload model '{modelIdentifier}' in {stopwatch.ElapsedMilliseconds}ms.", ex);
                return false;
            }
        }

        public async Task UnloadAllExceptAsync(string modelToKeep)
        {
            AppLogger.Info($"VRAM Cleanup: Ensuring only model '{modelToKeep}' remains loaded...");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string baseAddress = _apiUrl;
                if (baseAddress.EndsWith("/v1"))
                {
                    baseAddress = baseAddress.Substring(0, baseAddress.Length - 3);
                }

                var response = await _httpClient.GetAsync($"{baseAddress}/api/v1/models");
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.Warn($"VRAM Cleanup: Failed to fetch models list (Status: {response.StatusCode}).");
                    return;
                }

                string content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (!doc.RootElement.TryGetProperty("models", out var modelsElement))
                {
                    return;
                }

                foreach (var modelItem in modelsElement.EnumerateArray())
                {
                    string? key = modelItem.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                    if (string.IsNullOrEmpty(key)) continue;

                    if (!string.Equals(key, modelToKeep, StringComparison.OrdinalIgnoreCase))
                    {
                        if (modelItem.TryGetProperty("loaded_instances", out var instancesElement) && 
                            instancesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var instance in instancesElement.EnumerateArray())
                            {
                                string? instanceId = instance.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                                if (!string.IsNullOrEmpty(instanceId))
                                {
                                    AppLogger.Info($"VRAM Cleanup: Unloading non-matching active model '{key}' (instance: '{instanceId}')...");
                                    var payload = new { instance_id = instanceId };
                                    string jsonPayload = JsonSerializer.Serialize(payload);
                                    using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                                    var unloadResponse = await _httpClient.PostAsync($"{baseAddress}/api/v1/models/unload", httpContent);
                                    if (unloadResponse.IsSuccessStatusCode)
                                    {
                                        AppLogger.Info($"VRAM Cleanup: Successfully unloaded instance '{instanceId}'.");
                                    }
                                    else
                                    {
                                        string err = await unloadResponse.Content.ReadAsStringAsync();
                                        AppLogger.Warn($"VRAM Cleanup: Unload instance '{instanceId}' failed. Details: {err}");
                                    }
                                }
                            }
                        }
                    }
                }
                stopwatch.Stop();
                AppLogger.Debug($"VRAM Cleanup: Completed in {stopwatch.ElapsedMilliseconds}ms.");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"VRAM Cleanup: Model cleanup failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            AppLogger.Debug("LmStudioClient: Disposed.");
        }
    }
}
