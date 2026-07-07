using System;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class TranslatorService
    {
        private readonly LmStudioClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;
        private readonly string _targetLanguage;

        public TranslatorService(LmStudioClient client, string systemPrompt, string? modelName = null, string targetLanguage = "Spanish")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            _modelName = modelName;
            _targetLanguage = targetLanguage;
        }

        public async Task<string> TranslateTextAsync(string sourceText, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return $"*Page {pageNumber} was empty.*";
            }

            string userPrompt = $"Translate the following text into {_targetLanguage}. Make sure to only return the translation, preserving format:\n\n{sourceText}";

            Console.Write($"Translating page {pageNumber}... ");

            string translatedText = await _client.SendTextRequestAsync(
                _systemPrompt,
                userPrompt,
                _modelName
            );

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done.");
            Console.ResetColor();

            return translatedText;
        }
    }
}
