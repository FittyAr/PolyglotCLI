using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public interface IDocumentExtractor
    {
        bool CanHandle(string fileExtension);

        /// <summary>
        /// Extrae el texto del documento por página/chunk.
        /// </summary>
        /// <param name="context">
        /// Contexto de la ejecución (carpeta del job, callbacks, cancelación).
        /// Null equivale a <see cref="ExtractionContext.None"/>.
        /// </param>
        /// <param name="cachedStates">
        /// Estados guardados de una ejecución previa. Los extractores
        /// costosos (OCR) los usan para no repetir trabajo; la fusión de
        /// traducciones previas la hace el orquestador de forma central.
        /// </param>
        Task<List<PageProcessState>> ExtractTextAsync(
            string filePath,
            DocumentTarget target,
            OcrService ocrService,
            PdfPageRenderer pageRenderer,
            ExtractionContext? context = null,
            List<PageProcessState>? cachedStates = null);
    }
}
