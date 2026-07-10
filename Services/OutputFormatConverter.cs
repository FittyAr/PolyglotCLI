using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Markdig;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlToOpenXml;
using PeachPDF;
using NetOdt;
using NetOdt.Enumerations;

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
        /// Uses pandoc if available, and falls back to local C# libraries (HtmlToOpenXml, PeachPDF, NetOdt).
        /// </summary>
        public static async Task ConvertToFormatsAsync(string markdownPath, string outputFormats)
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
                    else if (fmt == "docx" || fmt == "odt" || fmt == "odf" || fmt == "pdf")
                    {
                        string pandocFormat = fmt == "odf" ? "odt" : fmt;
                        // Try pandoc first
                        if (!TryConvertWithPandoc(markdownPath, outputPath, pandocFormat))
                        {
                            AppLogger.Warn($"Format Conversion: pandoc not available for {fmt.ToUpperInvariant()}. Falling back to local C# OpenSource conversion...");
                            
                            // Generate HTML helper string for HTML-based converters
                            string tempHtmlPath = $"{basePath}_temp.html";
                            ConvertToHtml(markdownPath, tempHtmlPath);
                            string htmlContent = File.ReadAllText(tempHtmlPath, Encoding.UTF8);
                            
                            try
                            {
                                if (fmt == "docx")
                                {
                                    ConvertHtmlToDocxLocal(htmlContent, outputPath);
                                }
                                else if (fmt == "pdf")
                                {
                                    await ConvertHtmlToPdfLocalAsync(htmlContent, outputPath);
                                }
                                else if (fmt == "odt" || fmt == "odf")
                                {
                                    // Use ODF/ODT extension as defined
                                    string odtPath = fmt == "odf" ? $"{basePath}.odf" : $"{basePath}.odt";
                                    ConvertMarkdownToOdtLocal(markdownPath, odtPath);
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
            Console.WriteLine($"  → DOCX saved (locally) to: {outputPath}");
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
            Console.WriteLine($"  → PDF saved (locally) to: {outputPath}");
        }

        public static void ConvertMarkdownToOdtLocal(string markdownPath, string outputPath)
        {
            AppLogger.Info($"Local Format Conversion (Odt): Converting markdown to '{outputPath}'...");
            if (!File.Exists(markdownPath))
            {
                throw new FileNotFoundException($"Markdown file not found: {markdownPath}");
            }

            string[] lines = File.ReadAllLines(markdownPath, Encoding.UTF8);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            string exeDir = AppContext.BaseDirectory;
            string originalDir = Path.Combine(exeDir, "Original");
            string metaInfDir = Path.Combine(originalDir, "META-INF");
            string picturesDir = Path.Combine(originalDir, "Pictures");
            string thumbnailsDir = Path.Combine(originalDir, "Thumbnails");
            if (!Directory.Exists(originalDir)) Directory.CreateDirectory(originalDir);
            if (!Directory.Exists(metaInfDir)) Directory.CreateDirectory(metaInfDir);
            if (!Directory.Exists(picturesDir)) Directory.CreateDirectory(picturesDir);
            if (!Directory.Exists(thumbnailsDir)) Directory.CreateDirectory(thumbnailsDir);

            // Escribir mimetype
            string mimetypePath = Path.Combine(originalDir, "mimetype");
            if (!File.Exists(mimetypePath))
            {
                File.WriteAllText(mimetypePath, "application/vnd.oasis.opendocument.text", new UTF8Encoding(false));
            }

            // Escribir thumbnail.png
            string thumbnailPath = Path.Combine(thumbnailsDir, "thumbnail.png");
            if (!File.Exists(thumbnailPath))
            {
                File.WriteAllBytes(thumbnailPath, new byte[0]);
            }

            // Escribir manifest.xml
            string manifestPath = Path.Combine(metaInfDir, "manifest.xml");
            if (!File.Exists(manifestPath))
            {
                string manifestXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<manifest:manifest xmlns:manifest=""urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"" manifest:version=""1.2"">
  <manifest:file-entry manifest:full-path=""/"" manifest:version=""1.2"" manifest:media-type=""application/vnd.oasis.opendocument.text""/>
  <manifest:file-entry manifest:full-path=""content.xml"" manifest:media-type=""text/xml""/>
  <manifest:file-entry manifest:full-path=""styles.xml"" manifest:media-type=""text/xml""/>
  <manifest:file-entry manifest:full-path=""meta.xml"" manifest:media-type=""text/xml""/>
</manifest:manifest>";
                File.WriteAllText(manifestPath, manifestXml, Encoding.UTF8);
            }

            // Escribir content.xml
            string contentXmlPath = Path.Combine(originalDir, "content.xml");
            if (!File.Exists(contentXmlPath))
            {
                string contentXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<office:document-content xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:style=""urn:oasis:names:tc:opendocument:xmlns:style:1.0"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" xmlns:table=""urn:oasis:names:tc:opendocument:xmlns:table:1.0"" xmlns:draw=""urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"" xmlns:fo=""urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"" xmlns:xlink=""http://www.w3.org/1999/xlink"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"" xmlns:number=""urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"" xmlns:svg=""urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"" xmlns:chart=""urn:oasis:names:tc:opendocument:xmlns:chart:1.0"" xmlns:dr3d=""urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"" xmlns:math=""http://www.w3.org/1998/Math/MathML"" xmlns:form=""urn:oasis:names:tc:opendocument:xmlns:form:1.0"" xmlns:script=""urn:oasis:names:tc:opendocument:xmlns:script:1.0"" xmlns:ooo=""http://openoffice.org/2004/office"" xmlns:ooow=""http://openoffice.org/2004/writer"" xmlns:oooc=""http://openoffice.org/2004/calc"" xmlns:dom=""http://www.w3.org/2001/xml-events"" xmlns:xforms=""http://www.w3.org/2002/xforms"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:rpt=""http://openoffice.org/2009/report"" xmlns:of=""urn:oasis:names:tc:opendocument:xmlns:of:1.2"" xmlns:xhtml=""http://www.w3.org/1999/xhtml"" xmlns:grddl=""http://www.w3.org/2003/g/data-view#"" xmlns:officeooo=""http://openoffice.org/2009/office"" xmlns:tableooo=""http://openoffice.org/2009/table"" xmlns:drawooo=""http://openoffice.org/2009/draw"" xmlns:calcext=""urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"" xmlns:loext=""urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"" xmlns:field=""urn:openoffice:names:experimental:ooo-ms-format:drawing-陸"" office:version=""1.2"">
  <office:scripts/>
  <office:font-face-decls/>
  <office:automatic-styles/>
  <office:body>
    <office:text>
      <office:forms form:apply-design-mode=""false"" form:automatic-focus=""false""/>
    </office:text>
  </office:body>
</office:document-content>";
                File.WriteAllText(contentXmlPath, contentXml, Encoding.UTF8);
            }

            // Escribir styles.xml
            string stylesXmlPath = Path.Combine(originalDir, "styles.xml");
            if (!File.Exists(stylesXmlPath))
            {
                string stylesXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<office:document-styles xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:style=""urn:oasis:names:tc:opendocument:xmlns:style:1.0"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" xmlns:table=""urn:oasis:names:tc:opendocument:xmlns:table:1.0"" xmlns:draw=""urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"" xmlns:fo=""urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"" xmlns:xlink=""http://www.w3.org/1999/xlink"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"" xmlns:number=""urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"" xmlns:svg=""urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"" xmlns:chart=""urn:oasis:names:tc:opendocument:xmlns:chart:1.0"" xmlns:dr3d=""urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"" xmlns:math=""http://www.w3.org/1998/Math/MathML"" xmlns:form=""urn:oasis:names:tc:opendocument:xmlns:form:1.0"" xmlns:script=""urn:oasis:names:tc:opendocument:xmlns:script:1.0"" xmlns:ooo=""http://openoffice.org/2004/office"" xmlns:ooow=""http://openoffice.org/2004/writer"" xmlns:oooc=""http://openoffice.org/2004/calc"" xmlns:dom=""http://www.w3.org/2001/xml-events"" xmlns:rpt=""http://openoffice.org/2009/report"" xmlns:of=""urn:oasis:names:tc:opendocument:xmlns:of:1.2"" xmlns:xhtml=""http://www.w3.org/1999/xhtml"" xmlns:grddl=""http://www.w3.org/2003/g/data-view#"" xmlns:officeooo=""http://openoffice.org/2009/office"" xmlns:tableooo=""http://openoffice.org/2009/table"" xmlns:drawooo=""http://openoffice.org/2009/draw"" xmlns:calcext=""urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"" xmlns:loext=""urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"" xmlns:field=""urn:openoffice:names:experimental:ooo-ms-format:drawing-陸"" office:version=""1.2"">
  <office:font-face-decls/>
  <office:styles/>
  <office:automatic-styles/>
  <office:master-styles>
    <style:master-page style:name=""Standard"" style:page-layout-name=""Mpm1""/>
  </office:master-styles>
</office:document-styles>";
                File.WriteAllText(stylesXmlPath, stylesXml, Encoding.UTF8);
            }

            // Escribir meta.xml
            string metaXmlPath = Path.Combine(originalDir, "meta.xml");
            if (!File.Exists(metaXmlPath))
            {
                string metaXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<office:document-meta xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:xlink=""http://www.w3.org/1999/xlink"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"" xmlns:ooo=""http://openoffice.org/2004/office"" xmlns:grddl=""http://www.w3.org/2003/g/data-view#"" office:version=""1.2"">
  <office:meta>
    <meta:generator>NetOdt</meta:generator>
    <meta:initial-creator>NetOdt</meta:initial-creator>
    <meta:creation-date>2026-07-10T12:00:00Z</meta:creation-date>
    <dc:creator>NetOdt</dc:creator>
    <dc:date>2026-07-10T12:00:00Z</dc:date>
  </office:meta>
</office:document-meta>";
                File.WriteAllText(metaXmlPath, metaXml, Encoding.UTF8);
            }

            using (var odtDocument = new OdtDocument(outputPath))
            {
                odtDocument.SetGlobalFont("Arial", NetOdt.Enumerations.FontSize.Size11);

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        odtDocument.AppendEmptyLines(1);
                        continue;
                    }

                    if (trimmed.StartsWith("# "))
                    {
                        odtDocument.AppendLine(trimmed.Substring(2), TextStyle.Title);
                    }
                    else if (trimmed.StartsWith("## "))
                    {
                        odtDocument.AppendLine(trimmed.Substring(3), TextStyle.Bold);
                    }
                    else if (trimmed.StartsWith("### "))
                    {
                        odtDocument.AppendLine(trimmed.Substring(4), TextStyle.Bold | TextStyle.Italic);
                    }
                    else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    {
                        odtDocument.AppendLine("• " + trimmed.Substring(2), TextStyle.None);
                    }
                    else
                    {
                        odtDocument.AppendLine(line, TextStyle.None);
                    }
                }
            }
            AppLogger.Info($"Local Format Conversion (Odt): Succeeded saving odt to '{outputPath}'");
            Console.WriteLine($"  → ODT/ODF saved (locally) to: {outputPath}");
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
