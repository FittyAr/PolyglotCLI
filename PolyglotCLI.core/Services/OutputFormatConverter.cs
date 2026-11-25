using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlToOpenXml;
using PeachPDF;

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
            AppLogger.Info($"  → HTML saved to: {outputPath}");
        }

        /// <summary>
        /// Tiempo máximo que se le da a pandoc por conversión.
        /// </summary>
        public static readonly TimeSpan PandocTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Attempts to convert a Markdown file using pandoc (external tool).
        /// Returns true if successful, false if pandoc is not available or failed.
        /// stdout/stderr se leen de forma asíncrona (evita el deadlock por
        /// buffer lleno) y si pandoc excede <see cref="PandocTimeout"/> se
        /// mata el árbol de procesos completo.
        /// </summary>
        public static async Task<bool> TryConvertWithPandocAsync(string markdownPath, string outputPath, string format, CancellationToken cancellationToken = default)
        {
            AppLogger.Info($"Format Conversion (Pandoc): Attempting to convert '{markdownPath}' to '{format}' -> '{outputPath}'...");
            var stopwatch = Stopwatch.StartNew();
            Process? process = null;
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(markdownPath);
                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(outputPath);
                startInfo.ArgumentList.Add("--from=markdown");
                startInfo.ArgumentList.Add($"--to={format}");

                process = Process.Start(startInfo);
                if (process == null)
                {
                    AppLogger.Warn("Format Conversion (Pandoc): Failed to start process (process was null).");
                    return false;
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(PandocTimeout);
                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    KillProcessTree(process);
                    if (cancellationToken.IsCancellationRequested) throw;
                    AppLogger.Warn($"Format Conversion (Pandoc): pandoc excedió {PandocTimeout.TotalSeconds:F0}s y fue terminado.");
                    return false;
                }

                string error = await stderrTask;
                await stdoutTask;
                stopwatch.Stop();

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    AppLogger.Info($"Format Conversion (Pandoc): Conversion to {format} succeeded in {stopwatch.ElapsedMilliseconds}ms.");
                    AppLogger.Info($"  → {format.ToUpperInvariant()} saved to: {outputPath}");
                    return true;
                }

                AppLogger.Error($"Format Conversion (Pandoc): pandoc to {format} conversion failed (Exit code: {process.ExitCode}). Error details: {error.Trim()}");
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLogger.Warn($"Format Conversion (Pandoc): pandoc tool is not installed or failed to execute after {stopwatch.ElapsedMilliseconds}ms. Exception: {ex.Message}");
                return false;
            }
            finally
            {
                process?.Dispose();
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx)
            {
                AppLogger.Warn($"Format Conversion (Pandoc): no se pudo terminar pandoc: {killEx.Message}");
            }
        }

        /// <summary>
        /// Converts Markdown to requested output formats.
        /// Uses pandoc if available, and falls back to local C# libraries (HtmlToOpenXml, PeachPDF, OdtFileWriter).
        /// Devuelve <c>true</c> sólo si TODOS los formatos pedidos se
        /// generaron; un fallo se loguea y se refleja en el resultado para
        /// que el caller no marque la conversión como completada.
        /// </summary>
        public static async Task<bool> ConvertToFormatsAsync(string markdownPath, string outputFormats, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(outputFormats))
            {
                return true;
            }

            AppLogger.Info($"Format Conversion: Initiating conversions for: {outputFormats}");

            string[] formats = outputFormats.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string basePath = Path.ChangeExtension(markdownPath, null);
            bool allSucceeded = true;

            foreach (string format in formats)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    else if (fmt == "docx" || fmt == "odt" || fmt == "odf" || fmt == "pdf")
                    {
                        string pandocFormat = fmt == "odf" ? "odt" : fmt;
                        // Try pandoc first
                        if (!await TryConvertWithPandocAsync(markdownPath, outputPath, pandocFormat, cancellationToken))
                        {
                            AppLogger.Warn($"Format Conversion: pandoc not available for {fmt.ToUpperInvariant()}. Falling back to local C# OpenSource conversion...");

                            if (fmt == "odt" || fmt == "odf")
                            {
                                ConvertMarkdownToOdtLocal(markdownPath, outputPath);
                            }
                            else
                            {
                                // Generate HTML helper string for HTML-based converters
                                string tempHtmlPath = $"{basePath}_temp.html";
                                ConvertToHtml(markdownPath, tempHtmlPath);
                                try
                                {
                                    string htmlContent = File.ReadAllText(tempHtmlPath, Encoding.UTF8);
                                    if (fmt == "docx")
                                    {
                                        ConvertHtmlToDocxLocal(htmlContent, outputPath);
                                    }
                                    else
                                    {
                                        await ConvertHtmlToPdfLocalAsync(htmlContent, outputPath);
                                    }
                                }
                                finally
                                {
                                    // Clean up temp html file
                                    if (File.Exists(tempHtmlPath))
                                    {
                                        File.Delete(tempHtmlPath);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        AppLogger.Warn($"Format Conversion: Unknown requested output format '{fmt}'. Skipping.");
                        allSucceeded = false;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AppLogger.Error($"Format Conversion: Exception during format conversion to '{fmt}'.", ex);
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        public static void ConvertHtmlToDocxLocal(string htmlContent, string outputPath)
        {
            AppLogger.Info($"Local Format Conversion (Docx): Converting html to '{outputPath}'...");
            using (var memoryStream = new MemoryStream())
            {
                using (var package = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    var mainPart = package.AddMainDocumentPart();
                    mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                        new DocumentFormat.OpenXml.Wordprocessing.Body()
                    );

                    var htmlConverter = new HtmlConverter(mainPart);
                    htmlConverter.Parse(htmlContent);

                    mainPart.Document.Save();
                }

                File.WriteAllBytes(outputPath, memoryStream.ToArray());
            }
            AppLogger.Info($"Local Format Conversion (Docx): Succeeded saving docx to '{outputPath}'");
            AppLogger.Info($"  → DOCX saved (locally) to: {outputPath}");
        }

        public static async Task ConvertHtmlToPdfLocalAsync(string htmlContent, string outputPath)
        {
            AppLogger.Info($"Local Format Conversion (Pdf): Converting html to '{outputPath}'...");
            var pdfConfig = new PdfGenerateConfig
            {
                PageSize = PeachPDF.PageSize.Letter,
                PageOrientation = PageOrientation.Portrait
            };
            var generator = new PdfGenerator();
            var document = await generator.GeneratePdf(htmlContent, pdfConfig);
            using (var fileStream = File.Create(outputPath))
            {
                document.Save(fileStream);
            }
            AppLogger.Info($"Local Format Conversion (Pdf): Succeeded saving pdf to '{outputPath}'");
            AppLogger.Info($"  → PDF saved (locally) to: {outputPath}");
        }

        /// <summary>
        /// Conversión local Markdown → ODT/ODF. Usa <see cref="OdtFileWriter"/>,
        /// que no necesita plantillas en disco (ver su documentación). Lanza
        /// excepción si falla, para que el caller lo registre como error.
        /// </summary>
        public static void ConvertMarkdownToOdtLocal(string markdownPath, string outputPath)
        {
            AppLogger.Info($"Local Format Conversion (Odt): Converting markdown to '{outputPath}'...");
            if (!File.Exists(markdownPath))
            {
                throw new FileNotFoundException($"Markdown file not found: {markdownPath}");
            }

            string[] lines = File.ReadAllLines(markdownPath, Encoding.UTF8);
            OdtFileWriter.WriteFromMarkdownLines(lines, outputPath);

            AppLogger.Info($"Local Format Conversion (Odt): Succeeded saving odt to '{outputPath}'");
            AppLogger.Info($"  → ODT/ODF saved (locally) to: {outputPath}");
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
