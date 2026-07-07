using System;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class OcrService
    {
        private readonly LmStudioClient _client;
        private readonly string _systemPrompt;
        private readonly string? _modelName;

        public OcrService(LmStudioClient client, string systemPrompt, string? modelName = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            _modelName = modelName;
        }

        public async Task<string> PerformOcrAsync(byte[] imageBytes, int pageNumber)
        {
            string userPrompt = $"Please transcribe the text from page {pageNumber}. Extract everything exactly as written.";
            
            // We assume PNG format as rendered by PdfPageRenderer
            string mimeType = "image/png";

            Console.Write($"Running OCR for page {pageNumber}... ");
            
            string transcribedText = await _client.SendVisionRequestAsync(
                _systemPrompt, 
                userPrompt, 
                imageBytes, 
                mimeType, 
                _modelName
            );

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done.");
            Console.ResetColor();

            return transcribedText;
        }
    }
}
