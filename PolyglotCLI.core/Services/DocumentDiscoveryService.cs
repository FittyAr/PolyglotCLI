using System;
using System.Collections.Generic;
using System.IO;

namespace PolyglotCLI
{
    public static class DocumentDiscoveryService
    {
        public static List<SelectableFile> ScanDirectory(string dirPath, AppConfig config)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                throw new ArgumentException("Directory path cannot be empty.");
            }

            if (!Directory.Exists(dirPath))
            {
                throw new DirectoryNotFoundException($"The directory '{dirPath}' does not exist.");
            }

            var result = new List<SelectableFile>();
            var files = Directory.GetFiles(dirPath);
            var supportedExtensions = new HashSet<string>(config.SupportedInputExtensions ?? new List<string>
            {
                ".pdf", ".docx", ".doc", ".odt", ".odf", ".txt", ".md", 
                ".json", ".csv", ".xml", ".html", ".jpg", ".jpeg", ".png", ".bmp", ".tiff"
            }, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (supportedExtensions.Contains(ext))
                {
                    result.Add(new SelectableFile
                    {
                        FullPath = Path.GetFullPath(file),
                        IsSelected = false,
                        Mode = "text",
                        PageRange = "all"
                    });
                }
            }
            return result;
        }
    }
}
