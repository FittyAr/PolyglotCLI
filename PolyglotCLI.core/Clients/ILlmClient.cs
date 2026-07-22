using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public interface ILlmClient : IDisposable
    {
        double Temperature { get; set; }
        Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName = null);
        Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string mimeType, string? modelName = null);
        Task<List<string>> GetAvailableModelsAsync();
        Task<string> GetFirstLoadedModelAsync();
        Task UnloadAllExceptAsync(string keepModelName);
        Task<bool> UnloadModelAsync(string modelName);
    }
}
