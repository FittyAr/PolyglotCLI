using System;
using System.IO;
using System.Text;

namespace PolyglotCLI
{
    public class MarkdownWriter
    {
        private string? _filePath;

        public void Initialize(string filePath, string documentTitle, string targetLanguage)
        {
            _filePath = filePath;

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create or overwrite the file with header details
            var header = new StringBuilder();
            if (targetLanguage.Equals("Original", StringComparison.OrdinalIgnoreCase))
            {
                header.AppendLine($"# Original Text of: {documentTitle}");
                header.AppendLine($"- **Language:** Original");
            }
            else
            {
                header.AppendLine($"# Translation of: {documentTitle}");
                header.AppendLine($"- **Target Language:** {targetLanguage}");
            }
            header.AppendLine($"- **Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine("- - -");
            header.AppendLine();

            File.WriteAllText(_filePath, header.ToString(), Encoding.UTF8);
            Console.WriteLine($"Initialized output file: {filePath}");
        }

        public void AppendPage(int pageNumber, string translatedText)
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                throw new InvalidOperationException("MarkdownWriter has not been initialized.");
            }

            var content = new StringBuilder();
            content.AppendLine($"## Page {pageNumber}");
            content.AppendLine(translatedText);
            content.AppendLine();
            content.AppendLine("- - -");
            content.AppendLine();

            File.AppendAllText(_filePath, content.ToString(), Encoding.UTF8);
            Console.WriteLine($"Saved page {pageNumber} translation to markdown.");
        }

        public void UpdatePage(int pageNumber, string newTranslatedText)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string fileContent = File.ReadAllText(_filePath, Encoding.UTF8);
                string pageHeader = $"## Page {pageNumber}";
                int headerIdx = fileContent.IndexOf(pageHeader);

                if (headerIdx == -1)
                {
                    // Fallback: if not found, just append
                    AppendPage(pageNumber, newTranslatedText);
                    return;
                }

                // Find the next separator "- - -" after the header
                int separatorIdx = fileContent.IndexOf("- - -", headerIdx);
                if (separatorIdx == -1)
                {
                    separatorIdx = fileContent.Length;
                }

                // Construct the new page block (without the trailing separator, as it is already in the file)
                var newBlock = new StringBuilder();
                newBlock.AppendLine($"## Page {pageNumber}");
                newBlock.AppendLine(newTranslatedText);
                newBlock.AppendLine();

                string before = fileContent.Substring(0, headerIdx);
                string after = fileContent.Substring(separatorIdx);

                string updatedContent = before + newBlock.ToString() + after;
                File.WriteAllText(_filePath, updatedContent, Encoding.UTF8);
                Console.WriteLine($"[RETRY] Updated page {pageNumber} in markdown.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error updating page {pageNumber} in markdown: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
