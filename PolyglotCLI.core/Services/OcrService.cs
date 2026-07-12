using System;
using System.Diagnostics;
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
            string mimeType = "image/png";

            AppLogger.Info($"OCR Process page {pageNumber}: Starting vision request ({imageBytes.Length / 1024.0:F2} KB)...");
            Console.Write($"Running OCR for page {pageNumber}... ");
            
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
                AppLogger.Info($"OCR Process page {pageNumber}: Succeeded in {stopwatch.ElapsedMilliseconds}ms. Length: {transcribedText.Length} chars.");
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done.");
                Console.ResetColor();

                return transcribedText;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Error($"OCR Process page {pageNumber}: Failed after {stopwatch.ElapsedMilliseconds}ms.", ex);
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed.");
                Console.ResetColor();
                throw;
            }
        }
    }
}
