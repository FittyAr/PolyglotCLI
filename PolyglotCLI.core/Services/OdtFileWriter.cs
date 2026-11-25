using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PolyglotCLI
{
    /// <summary>
    /// Generador mínimo de documentos OpenDocument Text (.odt/.odf) a
    /// partir de Markdown simple (títulos, viñetas y párrafos).
    ///
    /// <para>Reemplaza a NetOdt, que arma su plantilla en una carpeta
    /// "Original" junto al ensamblado (AppContext.BaseDirectory): esa
    /// carpeta es de sólo lectura en MSIX / Program Files y no existe en
    /// publicaciones single-file, por lo que la conversión fallaba. Este
    /// writer escribe el paquete ZIP directamente en memoria y lo vuelca
    /// al destino, sin tocar ninguna carpeta de la instalación.</para>
    /// </summary>
    public static class OdtFileWriter
    {
        private const string MimeType = "application/vnd.oasis.opendocument.text";
        private const string FontName = "Arial";
        private const string BodyFontSize = "11pt";
        private const string TitleFontSize = "18pt";
        private const string Heading2FontSize = "14pt";
        private const string Heading3FontSize = "12pt";
        private const string Bullet = "• ";

        private enum ParagraphKind { Body, Title, Heading2, Heading3, Bullet, Empty }

        /// <summary>
        /// Convierte las líneas Markdown en un .odt escrito en
        /// <paramref name="outputPath"/> (se sobrescribe si existe).
        /// </summary>
        public static void WriteFromMarkdownLines(IEnumerable<string> markdownLines, string outputPath)
        {
            var paragraphs = new List<(ParagraphKind Kind, string Text)>();
            foreach (var line in markdownLines)
            {
                paragraphs.Add(Classify(line));
            }

            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                // ODF exige que "mimetype" sea la primera entrada y vaya sin comprimir.
                WriteEntry(zip, "mimetype", MimeType, CompressionLevel.NoCompression);
                WriteEntry(zip, "META-INF/manifest.xml", BuildManifestXml(), CompressionLevel.Optimal);
                WriteEntry(zip, "meta.xml", BuildMetaXml(), CompressionLevel.Optimal);
                WriteEntry(zip, "styles.xml", BuildStylesXml(), CompressionLevel.Optimal);
                WriteEntry(zip, "content.xml", BuildContentXml(paragraphs), CompressionLevel.Optimal);
            }

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputPath, buffer.ToArray());
        }

        private static (ParagraphKind, string) Classify(string line)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) return (ParagraphKind.Empty, string.Empty);
            if (trimmed.StartsWith("### ", StringComparison.Ordinal)) return (ParagraphKind.Heading3, trimmed.Substring(4));
            if (trimmed.StartsWith("## ", StringComparison.Ordinal)) return (ParagraphKind.Heading2, trimmed.Substring(3));
            if (trimmed.StartsWith("# ", StringComparison.Ordinal)) return (ParagraphKind.Title, trimmed.Substring(2));
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                return (ParagraphKind.Bullet, Bullet + trimmed.Substring(2));
            }
            return (ParagraphKind.Body, line.TrimEnd());
        }

        private static void WriteEntry(ZipArchive zip, string name, string content, CompressionLevel level)
        {
            var entry = zip.CreateEntry(name, level);
            using var stream = entry.Open();
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string StyleNameFor(ParagraphKind kind) => kind switch
        {
            ParagraphKind.Title => "PTitle",
            ParagraphKind.Heading2 => "PHeading2",
            ParagraphKind.Heading3 => "PHeading3",
            _ => "PBody"
        };

        private static string BuildContentXml(List<(ParagraphKind Kind, string Text)> paragraphs)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" ");
            sb.Append("xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" ");
            sb.Append("xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" ");
            sb.Append("xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" ");
            sb.Append("xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.2\">");
            sb.Append("<office:font-face-decls>");
            sb.Append($"<style:font-face style:name=\"{FontName}\" svg:font-family=\"{FontName}\"/>");
            sb.Append("</office:font-face-decls>");
            sb.Append("<office:automatic-styles>");
            AppendParagraphStyle(sb, "PBody", BodyFontSize, bold: false, italic: false);
            AppendParagraphStyle(sb, "PTitle", TitleFontSize, bold: true, italic: false);
            AppendParagraphStyle(sb, "PHeading2", Heading2FontSize, bold: true, italic: false);
            AppendParagraphStyle(sb, "PHeading3", Heading3FontSize, bold: true, italic: true);
            sb.Append("</office:automatic-styles>");
            sb.Append("<office:body><office:text>");
            foreach (var (kind, text) in paragraphs)
            {
                sb.Append($"<text:p text:style-name=\"{StyleNameFor(kind)}\">");
                sb.Append(EscapeXml(text));
                sb.Append("</text:p>");
            }
            sb.Append("</office:text></office:body></office:document-content>");
            return sb.ToString();
        }

        private static void AppendParagraphStyle(StringBuilder sb, string name, string size, bool bold, bool italic)
        {
            sb.Append($"<style:style style:name=\"{name}\" style:family=\"paragraph\">");
            sb.Append($"<style:text-properties style:font-name=\"{FontName}\" fo:font-size=\"{size}\"");
            if (bold) sb.Append(" fo:font-weight=\"bold\"");
            if (italic) sb.Append(" fo:font-style=\"italic\"");
            sb.Append("/></style:style>");
        }

        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
                   "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" office:version=\"1.2\">" +
                   "<office:styles/><office:automatic-styles>" +
                   "<style:page-layout style:name=\"Mpm1\"><style:page-layout-properties fo:margin-top=\"2cm\" fo:margin-bottom=\"2cm\" fo:margin-left=\"2cm\" fo:margin-right=\"2cm\"/></style:page-layout>" +
                   "</office:automatic-styles>" +
                   "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles>" +
                   "</office:document-styles>";
        }

        private static string BuildMetaXml()
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
                   "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" " +
                   "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" office:version=\"1.2\">" +
                   "<office:meta><meta:generator>PolyglotCLI</meta:generator>" +
                   $"<meta:creation-date>{now}</meta:creation-date><dc:date>{now}</dc:date></office:meta>" +
                   "</office:document-meta>";
        }

        private static string BuildManifestXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\">" +
                   $"<manifest:file-entry manifest:full-path=\"/\" manifest:version=\"1.2\" manifest:media-type=\"{MimeType}\"/>" +
                   "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
                   "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>" +
                   "<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>" +
                   "</manifest:manifest>";
        }

        /// <summary>
        /// Escapa texto para XML 1.0 y descarta caracteres de control que
        /// XML no admite (dejarían el .odt ilegible).
        /// </summary>
        private static string EscapeXml(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    case '\t': sb.Append("<text:tab/>"); break;
                    default:
                        if (c >= 0x20 || c == '\n' || c == '\r') sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
