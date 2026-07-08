using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class DocDocumentExtractor : IDocumentExtractor
    {
        public bool CanHandle(string fileExtension)
        {
            return fileExtension.Equals(".doc", StringComparison.OrdinalIgnoreCase);
        }

        public Task<List<PageProcessState>> ExtractTextAsync(string filePath, DocumentTarget target, OcrService ocrService, PdfPageRenderer pageRenderer)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
                string text = ScrapeTextFromDoc(filePath);
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
            catch (Exception ex)
            {
                pageStates.Add(new PageProcessState
                {
                    PageNumber = 1,
                    OcrFailed = true,
                    OcrErrorMessage = ex.Message,
                    OcrText = $"*Failed to extract text from Doc file: {ex.Message}*"
                });
            }

            return Task.FromResult(pageStates);
        }

        private string ScrapeTextFromDoc(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            
            var asciiSb = new StringBuilder();
            var tempAscii = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if ((b >= 32 && b <= 126) || b == 10 || b == 13 || b == 9 || b >= 160)
                {
                    tempAscii.Append((char)b);
                }
                else
                {
                    if (tempAscii.Length >= 6)
                    {
                        asciiSb.AppendLine(tempAscii.ToString().Trim());
                    }
                    tempAscii.Clear();
                }
            }
            if (tempAscii.Length >= 6)
            {
                asciiSb.AppendLine(tempAscii.ToString().Trim());
            }

            var utf16Sb = new StringBuilder();
            var tempUtf16 = new StringBuilder();
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                char c = (char)BitConverter.ToUInt16(bytes, i);
                if ((c >= 32 && c <= 126) || c == '\n' || c == '\r' || c == '\t' || (c >= 160 && c < 0xD7FF))
                {
                    tempUtf16.Append(c);
                }
                else
                {
                    if (tempUtf16.Length >= 6)
                    {
                        utf16Sb.AppendLine(tempUtf16.ToString().Trim());
                    }
                    tempUtf16.Clear();
                }
            }
            if (tempUtf16.Length >= 6)
            {
                utf16Sb.AppendLine(tempUtf16.ToString().Trim());
            }

            string txtAscii = asciiSb.ToString();
            string txtUtf16 = utf16Sb.ToString();

            return txtUtf16.Length > txtAscii.Length ? txtUtf16 : txtAscii;
        }
    }
}
