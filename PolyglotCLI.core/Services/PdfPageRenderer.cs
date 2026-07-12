using System;
using System.IO;
using PDFtoImage;
using SkiaSharp;

namespace PolyglotCLI
{
    public class PdfPageRenderer
    {
        public byte[] RenderPageToPng(string pdfPath, int pageNumber)
        {
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"PDF file not found: {pdfPath}");
            }

            if (pageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be 1 or greater.");
            }

            // PDFtoImage uses 0-based indexing for pages
            int pageIndex = pageNumber - 1;

            using (var fileStream = File.OpenRead(pdfPath))
#pragma warning disable CA1416
            using (SKBitmap bitmap = Conversion.ToImage(fileStream, pageIndex))
#pragma warning restore CA1416
            using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }
    }
}
