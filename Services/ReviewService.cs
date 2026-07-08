using System;
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
                return translatedText ?? string.Empty;
            }

            string userPrompt = $"--- ORIGINAL TEXT ---\n{originalText}\n\n--- TRANSLATED TEXT ---\n{translatedText}";

            Console.Write($"Reviewing page {pageNumber}... ");

            string reviewedText = await _client.SendTextRequestAsync(
                _systemPrompt,
                userPrompt,
                _modelName
            );

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done.");
            Console.ResetColor();

            return reviewedText;
        }
    }
}
