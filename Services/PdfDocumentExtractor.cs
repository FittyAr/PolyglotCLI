using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class PdfDocumentExtractor : IDocumentExtractor
    {
        public bool CanHandle(string fileExtension)
        {
            return fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
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
            using var textExtractor = new PdfTextExtractor();
            
            textExtractor.Open(filePath);
            int totalPages = textExtractor.PageCount;

            HashSet<int>? pageFilter = CommandLineOptions.ParsePageRange(target.PageRange, totalPages);

            var resolvedPages = new List<int>();
            for (int p = 1; p <= totalPages; p++)
            {
                if (pageFilter == null || pageFilter.Contains(p))
                {
                    resolvedPages.Add(p);
                }
            }

            foreach (int pageNum in resolvedPages)
            {
                var state = new PageProcessState { PageNumber = pageNum };
                pageStates.Add(state);

                var cached = cachedStates?.Find(s => s.PageNumber == pageNum);
                if (cached != null && !cached.OcrFailed && !string.IsNullOrEmpty(cached.OcrText))
                {
                    state.OcrText = cached.OcrText;
                    state.OcrFailed = false;
                    state.TranslatedText = cached.TranslatedText;
                    state.TranslationFailed = cached.TranslationFailed;
                    state.TranslationErrorMessage = cached.TranslationErrorMessage;
                    state.ReviewedText = cached.ReviewedText;
                    state.ReviewFailed = cached.ReviewFailed;
                    state.ReviewErrorMessage = cached.ReviewErrorMessage;
                    AppLogger.Info($"Page {pageNum}: Using cached OCR text from disk.");
                    continue;
                }

                if (target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        byte[] pngBytes = pageRenderer.RenderPageToPng(filePath, pageNum);
                        state.OcrText = await ocrService.PerformOcrAsync(pngBytes, pageNum);
                        state.OcrFailed = false;
                    }
                    catch (Exception ocrEx)
                    {
                        state.OcrFailed = true;
                        state.OcrErrorMessage = ocrEx.Message;
                        state.OcrText = $"*Failed to perform OCR on page {pageNum} due to error: {ocrEx.Message}*";
                    }
                }
                else
                {
                    try
                    {
                        state.OcrText = textExtractor.ExtractTextFromPage(pageNum);
                        state.OcrFailed = false;
                    }
                    catch (Exception extractEx)
                    {
                        state.OcrFailed = true;
                        state.OcrErrorMessage = extractEx.Message;
                        state.OcrText = $"*Failed to extract text on page {pageNum} due to error: {extractEx.Message}*";
                    }
                }

                if (originalWriter != null)
                {
                    originalWriter.SaveOrUpdatePage(pageNum, state.OcrText ?? string.Empty);
                }
            }

            return pageStates;
        }
    }
}
