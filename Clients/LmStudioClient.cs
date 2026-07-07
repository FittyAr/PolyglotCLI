using System;
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

        public LmStudioClient(string apiUrl)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            // Default timeout to 5 minutes to accommodate large PDF pages or slow LLMs
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<string> GetFirstLoadedModelAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/models");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var dataElement = doc.RootElement.GetProperty("data");
                    if (dataElement.GetArrayLength() > 0)
                    {
                        return dataElement[0].GetProperty("id").GetString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to fetch loaded models from LM Studio: {ex.Message}");
                Console.ResetColor();
            }

            return string.Empty;
        }

        public async Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = await GetFirstLoadedModelAsync();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "default-model"; // fallback
                }
            }

            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", httpContent);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"LM Studio request failed with status code {response.StatusCode}. Details: {errorContent}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            
            string contentResult = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
                
            return contentResult;
        }

        public async Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string imageMimeType, string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = await GetFirstLoadedModelAsync();
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "default-model"; // fallback
                }
            }

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
                temperature = 0.2
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", httpContent);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"LM Studio vision request failed with status code {response.StatusCode}. Details: {errorContent}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }

        public async Task<bool> UnloadModelAsync(string modelIdentifier)
        {
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
                    return false;
                }

                string content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (!doc.RootElement.TryGetProperty("models", out var modelsElement))
                {
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
                                    var payload = new { instance_id = instanceId };
                                    string jsonPayload = JsonSerializer.Serialize(payload);
                                    using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                                    var unloadResponse = await _httpClient.PostAsync($"{baseAddress}/api/v1/models/unload", httpContent);
                                    if (unloadResponse.IsSuccessStatusCode)
                                    {
                                        Console.WriteLine($"[VRAM] Unloaded model instance '{instanceId}' from LM Studio.");
                                        anyUnloaded = true;
                                    }
                                }
                            }
                        }
                    }
                }

                return anyUnloaded;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to auto-unload model '{modelIdentifier}': {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        public async Task UnloadAllExceptAsync(string modelToKeep)
        {
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

                    // If it is NOT the model we want to keep, unload it
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
                                    var payload = new { instance_id = instanceId };
                                    string jsonPayload = JsonSerializer.Serialize(payload);
                                    using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                                    var unloadResponse = await _httpClient.PostAsync($"{baseAddress}/api/v1/models/unload", httpContent);
                                    if (unloadResponse.IsSuccessStatusCode)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        Console.WriteLine($"[VRAM Startup] Unloaded active model instance '{instanceId}' to free memory.");
                                        Console.ResetColor();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Startup model cleanup failed: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
