using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace PolyglotCLI
{
    public static class JobExportService
    {
        public static (int ExportedCount, List<string> Errors) ExportJobToMarkdown(string jobDir, AppConfig config)
        {
            var errors = new List<string>();
            var files = new List<string>();
            string dataDir = Path.Combine(jobDir, "data");
            if (Directory.Exists(dataDir))
            {
                files.AddRange(Directory.GetFiles(dataDir, "*_data.json"));
            }
            foreach (var file in Directory.GetFiles(jobDir, "*_data.json"))
            {
                if (!files.Contains(file)) files.Add(file);
            }

            // Ensure output directory exists
            if (!Directory.Exists(config.AbsoluteOutputDirectory))
            {
                try
                {
                    Directory.CreateDirectory(config.AbsoluteOutputDirectory);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to create output directory {config.AbsoluteOutputDirectory}: {ex.Message}");
                    return (0, errors);
                }
            }

            int count = 0;
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var pages = JsonSerializer.Deserialize<List<DocumentPageData>>(json);
                    if (pages != null && pages.Count > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file).Replace("_data", "");
                        
                        string originalOut = Path.Combine(config.AbsoluteOutputDirectory, $"{fileName}_original.md");
                        MarkdownWriter.ExportToMarkdown(originalOut, fileName, "Original", pages, true);

                        string translatedOut = Path.Combine(config.AbsoluteOutputDirectory, $"{fileName}_{config.TargetLanguage}.md");
                        MarkdownWriter.ExportToMarkdown(translatedOut, fileName, config.TargetLanguage, pages, false);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to export {Path.GetFileName(file)}: {ex.Message}");
                    AppLogger.Error($"Failed to export {file}", ex);
                }
            }
            return (count, errors);
        }
    }
}
