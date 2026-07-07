using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PolyglotCLI
{
    public class OdtDocumentExtractor : IDocumentExtractor
    {
        public bool CanHandle(string fileExtension)
        {
            return fileExtension.Equals(".odt", StringComparison.OrdinalIgnoreCase) || 
                   fileExtension.Equals(".odf", StringComparison.OrdinalIgnoreCase);
        }

        public Task<List<PageProcessState>> ExtractTextAsync(string filePath, DocumentTarget target, OcrService ocrService, PdfPageRenderer pageRenderer)
        {
            var pageStates = new List<PageProcessState>();
            try
            {
                var sb = new StringBuilder();
                using (ZipArchive archive = ZipFile.OpenRead(filePath))
                {
                    var entry = archive.GetEntry("content.xml");
                    if (entry == null)
                    {
                        throw new FileNotFoundException("Invalid ODT/ODF file: content.xml entry not found.");
                    }

                    using (var stream = entry.Open())
                    {
                        var xmlDoc = new XmlDocument();
                        xmlDoc.Load(stream);
                        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                        nsmgr.AddNamespace("text", "urn:oasis:names:tc:opendocument:xmlns:text:1.0");

                        // Select all text paragraphs and headings
                        var nodes = xmlDoc.SelectNodes("//text:p | //text:h", nsmgr);
                        if (nodes != null)
                        {
                            foreach (XmlNode node in nodes)
                            {
                                sb.AppendLine(node.InnerText);
                            }
                        }
                    }
                }

                string text = sb.ToString();
                var chunks = TextChunker.ChunkText(text);

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
                    OcrText = $"*Failed to read ODT/ODF file: {ex.Message}*"
                });
            }

            return Task.FromResult(pageStates);
        }
    }
}
