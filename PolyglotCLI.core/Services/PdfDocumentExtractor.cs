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
            ExtractionContext? context = null,
            List<PageProcessState>? cachedStates = null)
        {
            context ??= ExtractionContext.None;
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
                context.CancellationToken.ThrowIfCancellationRequested();
                var state = new PageProcessState { PageNumber = pageNum };
                pageStates.Add(state);

                // Reutilizar la extracción previa evita repetir el OCR
                // (caro). La fusión de traducciones/revisiones la hace el
                // orquestador (PageStateMerger) para todos los formatos.
                var cached = cachedStates?.Find(s => s.PageNumber == pageNum);
                if (cached != null && !cached.OcrFailed && !string.IsNullOrEmpty(cached.OcrText))
                {
                    state.OcrText = cached.OcrText;
                    state.ThoughtText = cached.ThoughtText;
                    state.OcrFailed = false;
                    AppLogger.Info($"Page {pageNum}: Using cached OCR text from disk.");
                    continue;
                }

                if (target.Mode.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        byte[] pngBytes = pageRenderer.RenderPageToPng(filePath, pageNum);
                        SaveRenderedPage(context.JobDirectory, filePath, pageNum, pngBytes);

                        var ocrRes = await ocrService.PerformOcrAsync(pngBytes, pageNum, context.CancellationToken);
                        state.OcrText = ocrRes.Text;
                        state.ThoughtText = ocrRes.Thought;
                        state.OcrFailed = false;
                        context.ReportPage(filePath, pageNum, true, null);
                    }
                    catch (Exception ocrEx) when (ocrEx is not OperationCanceledException && !LlmClientFactory.IsFatalAuthenticationError(ocrEx))
                    {
                        state.OcrFailed = true;
                        state.OcrErrorMessage = ocrEx.Message;
                        state.OcrText = $"*Failed to perform OCR on page {pageNum} due to error: {ocrEx.Message}*";
                        context.ReportPage(filePath, pageNum, false, ocrEx.Message);
                    }
                }
                else
                {
                    try
                    {
                        state.OcrText = textExtractor.ExtractTextFromPage(pageNum);
                        state.OcrFailed = false;
                        context.ReportPage(filePath, pageNum, true, null);
                    }
                    catch (Exception extractEx)
                    {
                        state.OcrFailed = true;
                        state.OcrErrorMessage = extractEx.Message;
                        state.OcrText = $"*Failed to extract text on page {pageNum} due to error: {extractEx.Message}*";
                        context.ReportPage(filePath, pageNum, false, extractEx.Message);
                    }
                }
            }

            return pageStates;
        }

        /// <summary>
        /// Guarda el PNG renderizado bajo {job}\temp\ si hay un job en
        /// curso (lo usa la UI para mostrar la página original).
        /// </summary>
        private static void SaveRenderedPage(string? jobDirectory, string filePath, int pageNum, byte[] pngBytes)
        {
            if (string.IsNullOrEmpty(jobDirectory)) return;
            try
            {
                string tempDir = Path.Combine(jobDirectory, "temp");
                Directory.CreateDirectory(tempDir);
                string docName = Path.GetFileNameWithoutExtension(filePath);
                string pngPath = Path.Combine(tempDir, $"{docName}_page_{pageNum}.png");
                File.WriteAllBytes(pngPath, pngBytes);
            }
            catch (Exception savePngEx)
            {
                AppLogger.Warn($"Failed to save temporary page PNG to temp directory: {savePngEx.Message}");
            }
        }
    }
}
