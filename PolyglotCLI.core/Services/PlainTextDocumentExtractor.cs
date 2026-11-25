using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class PlainTextDocumentExtractor : IDocumentExtractor
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".json", ".csv", ".xml", ".html"
        };

        public bool CanHandle(string fileExtension)
        {
            return SupportedExtensions.Contains(fileExtension);
        }

        public Task<List<PageProcessState>> ExtractTextAsync(
            string filePath, 
            DocumentTarget target, 
            OcrService ocrService, 
            PdfPageRenderer pageRenderer, 
            ExtractionContext? context = null,
            List<PageProcessState>? cachedStates = null)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
                (context ?? ExtractionContext.None).CancellationToken.ThrowIfCancellationRequested();
                string text = File.ReadAllText(filePath);
                var chunks = TextChunker.ChunkText(text, target.MaxCharactersPerChunk, target.ChunkOverlapCharacters);
                
                if (chunks.Count == 0)
                {
                    pageStates.Add(new PageProcessState
                    {
                        PageNumber = 1,
                        OcrText = string.Empty,
                        OcrFailed = false
                    });
                }
                else
                {
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        pageStates.Add(new PageProcessState
                        {
                            PageNumber = i + 1,
                            OcrText = chunks[i],
                            OcrFailed = false
                        });
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var state = new PageProcessState
                {
                    PageNumber = 1,
                    OcrFailed = true,
                    OcrErrorMessage = ex.Message,
                    OcrText = $"*Failed to read plain text file: {ex.Message}*"
                };
                pageStates.Add(state);
            }

            return Task.FromResult(pageStates);
        }
    }
}
