using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PolyglotCLI
{
    public class MarkdownWriter
    {
        public static void ExportToMarkdown(string filePath, string documentTitle, string targetLanguage, List<DocumentPageData> pages, bool isOriginal)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder();
            if (isOriginal)
            {
                sb.AppendLine($"# Source Document: {documentTitle}");
                sb.AppendLine("> Extracted text from original file.");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"# Translation Document: {documentTitle}");
                sb.AppendLine($"> Target Language: {targetLanguage}");
                sb.AppendLine($"> Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
            }

            // Ordenar una COPIA: la lista del caller no se modifica.
            var orderedPages = new List<DocumentPageData>(pages);
            orderedPages.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));

            foreach (var page in orderedPages)
            {
                string? text = isOriginal ? page.OriginalText : (!string.IsNullOrEmpty(page.ReviewedText) ? page.ReviewedText : page.TranslatedText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    sb.AppendLine(); // Separator, no "## Page X" string is added as requested
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
