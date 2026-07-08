using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public interface IDocumentExtractor
    {
        bool CanHandle(string fileExtension);
        Task<List<PageProcessState>> ExtractTextAsync(
            string filePath, 
            DocumentTarget target, 
            OcrService ocrService, 
            PdfPageRenderer pageRenderer, 
            List<PageProcessState>? cachedStates = null,
            MarkdownWriter? originalWriter = null);
    }
}
