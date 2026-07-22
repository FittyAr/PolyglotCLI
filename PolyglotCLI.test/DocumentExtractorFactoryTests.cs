using System;
using Xunit;
using PolyglotCLI;

namespace PolyglotCLI.test
{
    public class DocumentExtractorFactoryTests
    {
        private readonly DocumentExtractorFactory _factory;

        public DocumentExtractorFactoryTests()
        {
            _factory = new DocumentExtractorFactory();
        }

        [Theory]
        [InlineData("test.pdf", typeof(PdfDocumentExtractor))]
        [InlineData("test.txt", typeof(PlainTextDocumentExtractor))]
        [InlineData("test.md", typeof(PlainTextDocumentExtractor))]
        [InlineData("test.docx", typeof(DocxDocumentExtractor))]
        [InlineData("test.odt", typeof(OdtDocumentExtractor))]
        [InlineData("test.doc", typeof(DocDocumentExtractor))]
        [InlineData("test.png", typeof(ImageDocumentExtractor))]
        [InlineData("test.jpg", typeof(ImageDocumentExtractor))]
        [InlineData("test.jpeg", typeof(ImageDocumentExtractor))]
        [InlineData("test.bmp", typeof(ImageDocumentExtractor))]
        [InlineData("test.tiff", typeof(ImageDocumentExtractor))]
        [InlineData("test.unknown", typeof(PlainTextDocumentExtractor))] // Fallback check
        public void GetExtractor_ReturnsCorrectExtractorType(string filename, Type expectedType)
        {
            // Act
            var extractor = _factory.GetExtractor(filename);

            // Assert
            Assert.NotNull(extractor);
            Assert.IsType(expectedType, extractor);
        }
    }
}
