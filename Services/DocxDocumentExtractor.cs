using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PolyglotCLI
{
    public class DocxDocumentExtractor : IDocumentExtractor
    {
        public bool CanHandle(string fileExtension)
        {
            return fileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
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
                var sb = new StringBuilder();
                using (ZipArchive archive = ZipFile.OpenRead(filePath))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry == null)
                    {
                        throw new FileNotFoundException("Invalid DOCX file: word/document.xml entry not found.");
                    }

                    using (var stream = entry.Open())
                    {
                        var xmlDoc = new XmlDocument();
                        xmlDoc.Load(stream);
                        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                        nsmgr.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

                        var paragraphs = xmlDoc.SelectNodes("//w:p", nsmgr);
                        if (paragraphs != null)
                        {
                            foreach (XmlNode p in paragraphs)
                            {
                                var textNodes = p.SelectNodes(".//w:t", nsmgr);
                                if (textNodes != null)
                                {
                                    foreach (XmlNode t in textNodes)
                                    {
                                        sb.Append(t.InnerText);
                                    }
                                }
                                sb.AppendLine();
                            }
                        }
                    }
                }

                string text = sb.ToString();
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
                    OcrText = $"*Failed to read Docx file: {ex.Message}*"
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
