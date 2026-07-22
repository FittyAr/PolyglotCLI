using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class OcrResult
    {
        public string Text { get; set; } = string.Empty;
        public string? Thought { get; set; }
    }

    public class OcrService
    {
        private readonly ILlmClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;

        public OcrService(ILlmClient client, string systemPrompt, string? modelName = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            _modelName = modelName;
        }

        public async Task<OcrResult> PerformOcrAsync(byte[] imageBytes, int pageNumber)
        {
            string userPrompt = $"Please transcribe the text from page {pageNumber}. Extract everything exactly as written.";
            string mimeType = "image/png";

            AppLogger.Info($"OCR page {pageNumber}: Starting image request to model '{_modelName}'...");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string transcribedText = await _client.SendVisionRequestAsync(
                    _systemPrompt, 
                    userPrompt, 
                    imageBytes, 
                    mimeType, 
                    _modelName
                );
                stopwatch.Stop();
                AppLogger.Info($"OCR page {pageNumber}: Succeeded in {stopwatch.ElapsedMilliseconds}ms.");

                string cleanText = transcribedText;
                string? thought = null;

                var thinkMatch = System.Text.RegularExpressions.Regex.Match(transcribedText, @"<think>([\s\S]*?)</think>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (thinkMatch.Success)
                {
                    thought = thinkMatch.Groups[1].Value.Trim();
                    cleanText = System.Text.RegularExpressions.Regex.Replace(transcribedText, @"<think>[\s\S]*?</think>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                }

                return new OcrResult { Text = cleanText, Thought = thought };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"OCR page {pageNumber}: Failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                throw;
            }
        }
    }
}
