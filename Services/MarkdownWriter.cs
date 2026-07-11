using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PolyglotCLI
{
    public class MarkdownWriter
    {
        private string? _filePath;
        private readonly HashSet<int> _writtenPages = new HashSet<int>();

        public void InitializeOrKeep(string filePath, string documentTitle, string targetLanguage)
        {
            _filePath = filePath;
            _writtenPages.Clear();

            if (File.Exists(filePath))
            {
                Console.WriteLine($"Found existing output file: {filePath}. Resuming/Appending progress.");
                var existing = ReadPages(filePath);
                foreach (var key in existing.Keys)
                {
                    _writtenPages.Add(key);
                }
                return;
            }

            Initialize(filePath, documentTitle, targetLanguage);
        }

        public void SaveOrUpdatePage(int pageNumber, string content)
        {
            if (_writtenPages.Contains(pageNumber))
            {
                UpdatePage(pageNumber, content);
            }
            else
            {
                AppendPage(pageNumber, content);
                _writtenPages.Add(pageNumber);
            }
        }

        public static Dictionary<int, string> ReadPages(string filePath)
        {
            var pages = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return pages;
            }

            try
            {
                string fileContent = File.ReadAllText(filePath, Encoding.UTF8);
                string[] parts = fileContent.Split(new[] { "## Page " }, StringSplitOptions.None);
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    int firstNewLine = part.IndexOf('\n');
                    if (firstNewLine == -1) continue;

                    string pageNumStr = part.Substring(0, firstNewLine).Trim();
                    if (int.TryParse(pageNumStr, out int pageNum))
                    {
                        string pageContent = part.Substring(firstNewLine + 1);
                        int separatorIdx = pageContent.LastIndexOf("- - -");
                        if (separatorIdx != -1)
                        {
                            pageContent = pageContent.Substring(0, separatorIdx);
                        }
                        pages[pageNum] = pageContent.Trim('\r', '\n');
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error reading pages from {filePath}: {ex.Message}");
                Console.ResetColor();
            }

            return pages;
        }

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
                Console.WriteLine($"Updated page {pageNumber} in markdown.");
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
