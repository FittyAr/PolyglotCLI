using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class ReviewService
    {
        private readonly ILlmClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;

        public ReviewService(ILlmClient client, string systemPrompt, string? modelName = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            _modelName = modelName;
        }

        public async Task<string> ReviewTranslationAsync(string originalText, string translatedText, int pageNumber)
        {
            string cleanOriginal = System.Text.RegularExpressions.Regex.Replace(
                originalText ?? string.Empty, 
                @"<think>[\s\S]*?</think>", 
                string.Empty, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            ).Trim();

            string cleanTranslated = System.Text.RegularExpressions.Regex.Replace(
                translatedText ?? string.Empty, 
                @"<think>[\s\S]*?</think>", 
                string.Empty, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            ).Trim();

            if (string.IsNullOrWhiteSpace(cleanTranslated))
            {
                AppLogger.Warn($"Review page {pageNumber}: Translated text was empty (after think removal). Skipping API request.");
                return cleanTranslated;
            }

            string userPrompt = $"--- ORIGINAL TEXT ---\n{cleanOriginal}\n\n--- TRANSLATED TEXT ---\n{cleanTranslated}";

            AppLogger.Info($"Review page {pageNumber}: Starting text request to model '{_modelName}'...");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string reviewedText = await _client.SendTextRequestAsync(
                    _systemPrompt,
                    userPrompt,
                    _modelName
                );
                stopwatch.Stop();

                if (string.IsNullOrWhiteSpace(reviewedText))
                {
                    throw new InvalidOperationException("Review model returned an empty or whitespace response.");
                }

                AppLogger.Info($"Review page {pageNumber}: Succeeded in {stopwatch.ElapsedMilliseconds}ms.");

                return reviewedText;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"Review page {pageNumber}: Failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                throw;
            }
        }
    }
}
