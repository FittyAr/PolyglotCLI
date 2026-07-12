using System;
using System.Collections.Generic;
using System.IO;

namespace PolyglotCLI
{
    public class DocumentExtractorFactory
    {
        private readonly List<IDocumentExtractor> _extractors;

        public DocumentExtractorFactory()
        {
            _extractors = new List<IDocumentExtractor>
            {
                new PdfDocumentExtractor(),
                new PlainTextDocumentExtractor(),
                new DocxDocumentExtractor(),
                new OdtDocumentExtractor(),
                new ImageDocumentExtractor(),
                new DocDocumentExtractor()
            };
        }

        public IDocumentExtractor GetExtractor(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            foreach (var extractor in _extractors)
            {
                if (extractor.CanHandle(ext))
                {
                    return extractor;
                }
            }

            // Default fallback is plain text
            return new PlainTextDocumentExtractor();
        }
    }
}
