using System;
using System.Collections.Generic;
using UglyToad.PdfPig;

namespace PolyglotCLI
{
    public class PdfTextExtractor : IDisposable
    {
        private PdfDocument? _document;

        public void Open(string pdfPath)
        {
            if (_document != null)
            {
                Close();
            }
            _document = PdfDocument.Open(pdfPath);
        }

        public int PageCount
        {
            get
            {
                if (_document == null)
                {
                    throw new InvalidOperationException("No PDF document is open.");
                }
                return _document.NumberOfPages;
            }
        }

        public string ExtractTextFromPage(int pageNumber)
        {
            if (_document == null)
            {
                throw new InvalidOperationException("No PDF document is open.");
            }

            if (pageNumber < 1 || pageNumber > _document.NumberOfPages)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number is out of range.");
            }

            var page = _document.GetPage(pageNumber);
            return page.Text;
        }

        public void Close()
        {
            _document?.Dispose();
            _document = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
