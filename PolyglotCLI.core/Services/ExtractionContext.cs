using System;
using System.Threading;

namespace PolyglotCLI
{
    /// <summary>
    /// Contexto por-ejecución que se pasa a cada <see cref="IDocumentExtractor"/>.
    /// Reemplaza el estado estático que antes vivía en el orquestador
    /// (directorio del job, callback de OCR y token de cancelación): con
    /// estáticos, una extracción ajena (p.ej. el análisis de prompt de
    /// <see cref="PromptHelperService"/>) podía disparar los callbacks del
    /// job en curso o escribir PNGs en su carpeta temporal.
    /// </summary>
    public sealed class ExtractionContext
    {
        /// <summary>
        /// Carpeta del job en curso. Si es null, las páginas renderizadas
        /// no se persisten en disco.
        /// </summary>
        public string? JobDirectory { get; init; }

        /// <summary>Token de cancelación de la ejecución.</summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>
        /// Callback opcional por página: (rutaArchivo, página, éxito, error).
        /// </summary>
        public Action<string, int, bool, string?>? OnPageOcrCompleted { get; init; }

        /// <summary>Contexto vacío: sin job, sin callbacks, sin cancelación.</summary>
        public static ExtractionContext None { get; } = new ExtractionContext();

        internal void ReportPage(string filePath, int pageNumber, bool success, string? error)
        {
            OnPageOcrCompleted?.Invoke(filePath, pageNumber, success, error);
        }
    }
}
