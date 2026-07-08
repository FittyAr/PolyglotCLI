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
            List<PageProcessState>? cachedStates = null,
            MarkdownWriter? originalWriter = null)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
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

                if (originalWriter != null)
                {
                    foreach (var s in pageStates)
                    {
                        originalWriter.SaveOrUpdatePage(s.PageNumber, s.OcrText ?? string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                var state = new PageProcessState
                {
                    PageNumber = 1,
                    OcrFailed = true,
                    OcrErrorMessage = ex.Message,
                    OcrText = $"*Failed to read plain text file: {ex.Message}*"
                };
                pageStates.Add(state);

                if (originalWriter != null)
                {
                    originalWriter.SaveOrUpdatePage(1, state.OcrText ?? string.Empty);
                }
            }

            return Task.FromResult(pageStates);
        }
    }
}
