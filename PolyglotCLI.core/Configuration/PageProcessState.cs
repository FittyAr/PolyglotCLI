namespace PolyglotCLI
{
    public class PageProcessState
    {
        public int PageNumber { get; set; }
        public string? OcrText { get; set; }
        public string? TranslatedText { get; set; }
        public bool OcrFailed { get; set; }
        public bool TranslationFailed { get; set; }
        public string? OcrErrorMessage { get; set; }
        public string? TranslationErrorMessage { get; set; }
        public string? ReviewedText { get; set; }
        public bool ReviewFailed { get; set; }
        public string? ReviewErrorMessage { get; set; }
        public string? ThoughtText { get; set; }
    }
}
