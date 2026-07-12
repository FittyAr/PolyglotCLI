using System;
using System.IO;

namespace PolyglotCLI
{
    public class SelectableFile
    {
        public string FullPath { get; set; } = string.Empty;
        public string DisplayName => Path.GetFileName(FullPath);
        public bool IsSelected { get; set; }
        public string Mode { get; set; } = "text";
        public string PageRange { get; set; } = "all";

        public override string ToString()
        {
            string prefix = IsSelected ? "[X]" : "[ ]";
            
            string name = DisplayName;
            if (name.Length > 20)
            {
                name = name.Substring(0, 17) + "...";
            }
            name = name.PadRight(20);

            string ext = Path.GetExtension(FullPath).ToUpperInvariant().TrimStart('.');
            if (ext.Length > 4)
            {
                ext = ext.Substring(0, 4);
            }
            ext = ext.PadRight(4);

            string extLower = Path.GetExtension(FullPath).ToLowerInvariant();
            string mode = "";
            if (extLower == ".pdf")
            {
                mode = Mode.Equals("image", StringComparison.OrdinalIgnoreCase) 
                    ? "[ ] Text [X] Image" 
                    : "[X] Text [ ] Image";
            }
            else
            {
                bool isImage = extLower == ".jpg" || extLower == ".jpeg" || extLower == ".png" || extLower == ".bmp" || extLower == ".tiff";
                mode = isImage ? "          [X] Image" : "[X] Text           ";
            }
            mode = mode.PadRight(20);

            string pages = PageRange;
            if (extLower != ".pdf")
            {
                pages = "-";
            }
            pages = $"[ {pages} ]";
            if (pages.Length > 9)
            {
                pages = pages.Substring(0, 9);
            }
            pages = pages.PadRight(9);

            return $"{prefix} | {name} | {ext} | {mode} | {pages}";
        }
    }
}
