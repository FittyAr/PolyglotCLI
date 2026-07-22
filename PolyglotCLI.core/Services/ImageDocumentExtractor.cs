using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class ImageDocumentExtractor : IDocumentExtractor
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tiff"
        };

        public bool CanHandle(string fileExtension)
        {
            return SupportedExtensions.Contains(fileExtension);
        }

        public async Task<List<PageProcessState>> ExtractTextAsync(
            string filePath, 
            DocumentTarget target, 
            OcrService ocrService, 
            PdfPageRenderer pageRenderer, 
            List<PageProcessState>? cachedStates = null,
            MarkdownWriter? originalWriter = null)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
                var cached = cachedStates?.Find(s => s.PageNumber == 1);
                if (cached != null && !cached.OcrFailed && !string.IsNullOrEmpty(cached.OcrText))
                {
                    pageStates.Add(cached);
                    AppLogger.Info($"Image OCR: Using cached OCR text from disk.");
                    return pageStates;
                }

                byte[] imageBytes = File.ReadAllBytes(filePath);
                
                var ocrRes = await ocrService.PerformOcrAsync(imageBytes, 1);
                
                var state = new PageProcessState
                {
                    PageNumber = 1,
                    OcrText = ocrRes.Text,
                    ThoughtText = ocrRes.Thought,
                    OcrFailed = false
                };
                pageStates.Add(state);

                TranslationOrchestrator.OnPageOcrCompleted?.Invoke(filePath, 1, true, null);

                if (originalWriter != null)
                {
                    originalWriter.SaveOrUpdatePage(1, state.OcrText ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                if (LlmClientFactory.IsFatalAuthenticationError(ex))
                {
                    throw;
                }
                var state = new PageProcessState
                {
                    PageNumber = 1,
                    OcrFailed = true,
                    OcrErrorMessage = ex.Message,
                    OcrText = $"*Failed to perform OCR on image: {ex.Message}*"
                };
                pageStates.Add(state);

                TranslationOrchestrator.OnPageOcrCompleted?.Invoke(filePath, 1, false, ex.Message);

                if (originalWriter != null)
                {
                    originalWriter.SaveOrUpdatePage(1, state.OcrText ?? string.Empty);
                }
            }

            return pageStates;
        }
    }
}
