using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public interface ILlmClient : IDisposable
    {
        double Temperature { get; set; }
        Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName = null, CancellationToken cancellationToken = default);
        Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string mimeType, string? modelName = null, CancellationToken cancellationToken = default);
        Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
        Task<string> GetFirstLoadedModelAsync(CancellationToken cancellationToken = default);
        Task UnloadAllExceptAsync(string keepModelName, CancellationToken cancellationToken = default);
        Task<bool> UnloadModelAsync(string modelName, CancellationToken cancellationToken = default);
    }
}
