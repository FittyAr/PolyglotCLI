using System.Collections.Generic;

namespace PolyglotCLI
{
    /// <summary>
    /// Conversión entre el estado en memoria de una página
    /// (<see cref="PageProcessState"/>) y su forma persistida en
    /// <c>data/*_data.json</c> (<see cref="DocumentPageData"/>). Un único
    /// lugar evita que guardar/cargar se desincronicen (antes se perdían
    /// ThoughtText, ReviewFailed y ReviewErrorMessage).
    /// </summary>
    public static class PageStateMapper
    {
        public static DocumentPageData ToPageData(PageProcessState state)
        {
            return new DocumentPageData
            {
                PageNumber = state.PageNumber,
                OriginalText = state.OcrText,
                TranslatedText = state.TranslatedText,
                ReviewedText = state.ReviewedText,
                IsOcrSuccessful = !state.OcrFailed,
                IsTranslationSuccessful = !state.TranslationFailed,
                OcrErrorMessage = state.OcrErrorMessage,
                TranslationErrorMessage = state.TranslationErrorMessage,
                ThoughtText = state.ThoughtText,
                ReviewFailed = state.ReviewFailed,
                ReviewErrorMessage = state.ReviewErrorMessage
            };
        }

        public static PageProcessState ToPageState(DocumentPageData data)
        {
            return new PageProcessState
            {
                PageNumber = data.PageNumber,
                OcrText = data.OriginalText,
                TranslatedText = data.TranslatedText,
                ReviewedText = data.ReviewedText,
                OcrFailed = !data.IsOcrSuccessful,
                TranslationFailed = !data.IsTranslationSuccessful,
                OcrErrorMessage = data.OcrErrorMessage,
                TranslationErrorMessage = data.TranslationErrorMessage,
                ThoughtText = data.ThoughtText,
                ReviewFailed = data.ReviewFailed,
                ReviewErrorMessage = data.ReviewErrorMessage
            };
        }

        public static List<DocumentPageData> ToPageData(IEnumerable<PageProcessState> states)
        {
            var list = new List<DocumentPageData>();
            foreach (var s in states) list.Add(ToPageData(s));
            return list;
        }

        public static List<PageProcessState> ToPageStates(IEnumerable<DocumentPageData> data)
        {
            var list = new List<PageProcessState>();
            foreach (var d in data) list.Add(ToPageState(d));
            return list;
        }
    }
}
