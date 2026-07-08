using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using Markdig;

namespace PolyglotCLI
{
    public static class OutputFormatConverter
    {
        /// <summary>
        /// Converts a Markdown file to an HTML file with embedded styles.
        /// </summary>
        public static void ConvertToHtml(string markdownPath, string outputPath)
        {
            if (!File.Exists(markdownPath))
            {
                throw new FileNotFoundException($"Markdown file not found: {markdownPath}");
            }

            AppLogger.Info($"Format Conversion: Translating Markdown '{markdownPath}' to HTML '{outputPath}'...");
            var stopwatch = Stopwatch.StartNew();

            string markdown = File.ReadAllText(markdownPath, Encoding.UTF8);

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            string htmlBody = Markdig.Markdown.ToHtml(markdown, pipeline);

            string title = Path.GetFileNameWithoutExtension(markdownPath);
            string fullHtml = WrapInHtmlDocument(title, htmlBody);

            File.WriteAllText(outputPath, fullHtml, Encoding.UTF8);
            stopwatch.Stop();

            AppLogger.Info($"Format Conversion: HTML saved in {stopwatch.ElapsedMilliseconds}ms at {outputPath}");
            Console.WriteLine($"  → HTML saved to: {outputPath}");
        }

        /// <summary>
        /// Attempts to convert a Markdown file using pandoc (external tool).
        /// Returns true if successful, false if pandoc is not available.
        /// </summary>
        public static bool TryConvertWithPandoc(string markdownPath, string outputPath, string format)
        {
            AppLogger.Info($"Format Conversion (Pandoc): Attempting to convert '{markdownPath}' to '{format}' -> '{outputPath}'...");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    Arguments = $"\"{markdownPath}\" -o \"{outputPath}\" --from=markdown --to={format}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    AppLogger.Warn("Format Conversion (Pandoc): Failed to start process (process was null).");
                    return false;
                }

                process.WaitForExit(60000); // 60 second timeout
                stopwatch.Stop();

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    AppLogger.Info($"Format Conversion (Pandoc): Conversion to {format} succeeded in {stopwatch.ElapsedMilliseconds}ms.");
                    Console.WriteLine($"  → {format.ToUpperInvariant()} saved to: {outputPath}");
                    return true;
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    AppLogger.Error($"Format Conversion (Pandoc): pandoc to {format} conversion failed (Exit code: {process.ExitCode}). Error details: {error.Trim()}");
                    
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [WARNING] pandoc conversion to {format} failed: {error}");
                    Console.ResetColor();
                    return false;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Warn($"Format Conversion (Pandoc): pandoc tool is not installed or failed to execute after {stopwatch.ElapsedMilliseconds}ms. Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts Markdown to requested output formats.
        /// Uses pandoc for DOCX/ODT/PDF if available, falls back to HTML generation.
        /// </summary>
        public static void ConvertToFormats(string markdownPath, string outputFormats)
        {
            if (string.IsNullOrWhiteSpace(outputFormats))
            {
                return;
            }

            AppLogger.Info($"Format Conversion: Initiating conversions for: {outputFormats}");

            string[] formats = outputFormats.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string basePath = Path.ChangeExtension(markdownPath, null);

            foreach (string format in formats)
            {
                string fmt = format.Trim().ToLowerInvariant();

                if (fmt == "md" || fmt == "markdown")
                {
                    // Already saved as markdown, skip
                    continue;
                }

                string outputPath = $"{basePath}.{fmt}";

                try
                {
                    if (fmt == "html")
                    {
                        ConvertToHtml(markdownPath, outputPath);
                    }
                    else if (fmt == "docx" || fmt == "odt" || fmt == "pdf")
                    {
                        // Try pandoc first
                        if (!TryConvertWithPandoc(markdownPath, outputPath, fmt))
                        {
                            // Fallback: generate HTML instead
                            string htmlFallback = $"{basePath}.html";
                            AppLogger.Warn($"Format Conversion: pandoc not available for {fmt.ToUpperInvariant()}. Falling back to HTML format...");
                            
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  [INFO] pandoc not available for {fmt.ToUpperInvariant()} conversion. Generating HTML instead.");
                            Console.ResetColor();

                            if (!File.Exists(htmlFallback))
                            {
                                ConvertToHtml(markdownPath, htmlFallback);
                            }
                        }
                    }
                    else
                    {
                        AppLogger.Warn($"Format Conversion: Unknown requested output format '{fmt}'. Skipping.");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [WARNING] Unknown output format '{fmt}'. Skipping.");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Format Conversion: Exception during format conversion to '{fmt}'.", ex);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [ERROR] Failed to convert to {fmt}: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        private static string WrapInHtmlDocument(string title, string body)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 900px;
            margin: 0 auto;
            padding: 40px 20px;
            line-height: 1.6;
            color: #333;
            background: #fafafa;
        }}
        h1, h2, h3, h4, h5, h6 {{
            color: #2c3e50;
            margin-top: 1.5em;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 1em 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background-color: #f2f2f2;
            font-weight: bold;
        }}
        tr:nth-child(even) {{
            background-color: #f9f9f9;
        }}
        code {{
            background: #e8e8e8;
            padding: 2px 6px;
            border-radius: 3px;
            font-size: 0.9em;
        }}
        pre {{
            background: #2d2d2d;
            color: #f0f0f0;
            padding: 16px;
            border-radius: 6px;
            overflow-x: auto;
        }}
        pre code {{
            background: none;
            padding: 0;
            color: inherit;
        }}
        blockquote {{
            border-left: 4px solid #3498db;
            margin: 1em 0;
            padding: 0.5em 1em;
            background: #ecf0f1;
        }}
        hr {{
            border: none;
            border-top: 2px solid #eee;
            margin: 2em 0;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
    </style>
</head>
<body>
{body}
</body>
</html>";
        }
    }
}
