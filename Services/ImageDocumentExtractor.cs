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

        public async Task<List<PageProcessState>> ExtractTextAsync(string filePath, DocumentTarget target, OcrService ocrService, PdfPageRenderer pageRenderer)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                
                // Perform OCR on the image. Since it is a single image, page number is 1.
                string transcribedText = await ocrService.PerformOcrAsync(imageBytes, 1);
                
                pageStates.Add(new PageProcessState
                {
                    PageNumber = 1,
                    OcrText = transcribedText,
                    OcrFailed = false
                });
            }
            catch (Exception ex)
            {
                pageStates.Add(new PageProcessState
                {
                    PageNumber = 1,
                    OcrFailed = true,
                    OcrErrorMessage = ex.Message,
                    OcrText = $"*Failed to perform OCR on image: {ex.Message}*"
                });
            }

            return pageStates;
        }
    }
}
