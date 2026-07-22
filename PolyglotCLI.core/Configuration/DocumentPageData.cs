using System;

namespace PolyglotCLI
{
    public class DocumentPageData
    {
        public int PageNumber { get; set; }
        public string? OriginalText { get; set; }
        public string? TranslatedText { get; set; }
        public string? ReviewedText { get; set; }
        public bool IsOcrSuccessful { get; set; } = true;
        public bool IsTranslationSuccessful { get; set; } = true;
        public string? OcrErrorMessage { get; set; }
        public string? TranslationErrorMessage { get; set; }
        public double UsedTemperature { get; set; }
        public int RetryCount { get; set; }
        public string? ThoughtText { get; set; }
    }
}
