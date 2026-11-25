using System;
using System.Collections.Generic;

namespace PolyglotCLI
{
    /// <summary>
    /// Fusiona el resultado de una extracción nueva con los estados
    /// guardados de una ejecución previa (reanudar un job). Sin esta
    /// fusión, los extractores de texto (txt/docx/odt/doc) devolvían
    /// estados "limpios" y el orquestador los grababa encima de
    /// data.json, borrando las traducciones y revisiones ya hechas.
    ///
    /// <para>Regla: para cada página/chunk, si existe un estado previo
    /// con el MISMO número y el MISMO texto de origen, se conservan su
    /// traducción, revisión, razonamiento y flags. Si el texto cambió
    /// (archivo editado, otro tamaño de chunk) la traducción previa ya
    /// no corresponde y se descarta.</para>
    /// </summary>
    public static class PageStateMerger
    {
        /// <returns>Cantidad de páginas cuyo progreso previo se reutilizó.</returns>
        public static int MergeCachedProgress(List<PageProcessState> freshStates, List<PageProcessState>? cachedStates)
        {
            if (freshStates == null || cachedStates == null || cachedStates.Count == 0) return 0;

            var cachedByPage = new Dictionary<int, PageProcessState>();
            foreach (var cached in cachedStates)
            {
                cachedByPage[cached.PageNumber] = cached;
            }

            int reused = 0;
            foreach (var fresh in freshStates)
            {
                if (fresh.OcrFailed) continue;
                if (!cachedByPage.TryGetValue(fresh.PageNumber, out var cached)) continue;
                if (cached.OcrFailed) continue;
                if (!string.Equals(Normalize(fresh.OcrText), Normalize(cached.OcrText), StringComparison.Ordinal)) continue;

                bool hasProgress = !string.IsNullOrEmpty(cached.TranslatedText)
                                   || !string.IsNullOrEmpty(cached.ReviewedText)
                                   || cached.TranslationFailed
                                   || cached.ReviewFailed;

                fresh.TranslatedText = cached.TranslatedText;
                fresh.TranslationFailed = cached.TranslationFailed;
                fresh.TranslationErrorMessage = cached.TranslationErrorMessage;
                fresh.ReviewedText = cached.ReviewedText;
                fresh.ReviewFailed = cached.ReviewFailed;
                fresh.ReviewErrorMessage = cached.ReviewErrorMessage;
                if (string.IsNullOrEmpty(fresh.ThoughtText))
                {
                    fresh.ThoughtText = cached.ThoughtText;
                }

                if (hasProgress) reused++;
            }
            return reused;
        }

        private static string Normalize(string? text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        }
    }
}
