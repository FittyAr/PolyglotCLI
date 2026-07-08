using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class ReviewService
    {
        private readonly LmStudioClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;

        public ReviewService(LmStudioClient client, string systemPrompt, string? modelName = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            _modelName = modelName;
        }

        public async Task<string> ReviewTranslationAsync(string originalText, string translatedText, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                AppLogger.Warn($"Review page {pageNumber}: Translated text was empty. Skipping API request.");
                return translatedText ?? string.Empty;
            }

            string userPrompt = $"--- ORIGINAL TEXT ---\n{originalText}\n\n--- TRANSLATED TEXT ---\n{translatedText}";

            AppLogger.Info($"Review page {pageNumber}: Starting text request to model '{_modelName}' (Input length: {translatedText.Length} chars)...");
            Console.Write($"Reviewing page {pageNumber}... ");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string reviewedText = await _client.SendTextRequestAsync(
                    _systemPrompt,
                    userPrompt,
                    _modelName
                );
                stopwatch.Stop();
                AppLogger.Info($"Review page {pageNumber}: Succeeded in {stopwatch.ElapsedMilliseconds}ms. Output length: {reviewedText.Length} chars.");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done.");
                Console.ResetColor();

                return reviewedText;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"Review page {pageNumber}: Failed after {stopwatch.ElapsedMilliseconds}ms.", ex);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed.");
                Console.ResetColor();
                throw;
            }
        }
    }
}
