using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class TranslatorService
    {
        private readonly ILlmClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;
        private readonly string _targetLanguage;
        public bool PreserveFormat { get; set; } = true;

        public TranslatorService(ILlmClient client, string systemPrompt, string? modelName = null, string targetLanguage = "Spanish")
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
                AppLogger.Warn($"Translation page {pageNumber}: Source text was empty. Skipping API request.");
                return $"*Page {pageNumber} was empty.*";
            }

            string formatInstruction = PreserveFormat
                ? "Preserve all Markdown formatting (headers, tables, lists, bold, italic, links)."
                : "Translate only the text content. Do not preserve Markdown formatting, output plain text.";

            string userPrompt = $"Translate the following text into {_targetLanguage}. {formatInstruction} Make sure to only return the translation:\n\n{sourceText}";

            AppLogger.Info($"Translation page {pageNumber}: Starting text request to model '{_modelName}' (Input length: {sourceText.Length} chars)...");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string translatedText = await _client.SendTextRequestAsync(
                    _systemPrompt,
                    userPrompt,
                    _modelName
                );
                stopwatch.Stop();

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    throw new InvalidOperationException("Translation model returned an empty or whitespace response.");
                }

                AppLogger.Info($"Translation page {pageNumber}: Succeeded in {stopwatch.ElapsedMilliseconds}ms. Output length: {translatedText.Length} chars.");

                return translatedText;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"Translation page {pageNumber}: Failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                throw;
            }
        }
    }
}
