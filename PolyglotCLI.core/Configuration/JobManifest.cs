using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PolyglotCLI
{
    public class JobManifest
    {
        public string JobId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "InProgress"; // "InProgress", "Completed", "Failed"
        
        // Configuration stored directly
        public string TargetLanguage { get; set; } = "Spanish";
        public string Mode { get; set; } = "text";
        public string OutputDirectory { get; set; } = "output";
        public string PageRange { get; set; } = "all";
        public string? ModelName { get; set; }
        public string? VisionModelName { get; set; }
        public string? AdditionalPrompt { get; set; }
        public bool Transcribe { get; set; } = true;
        public bool Translate { get; set; } = true;
        public bool Verify { get; set; } = false;
        public bool GenerateDoc { get; set; } = false;
        public string? SelectedFormat { get; set; }

        public List<JobFileManifest> Files { get; set; } = new();

        public static JobManifest Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<JobManifest>(json) ?? new JobManifest();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to load manifest from {filePath}: {ex.Message}");
            }
            return new JobManifest();
        }

        public void Save(string filePath)
        {
            try
            {
                LastUpdatedAt = DateTime.Now;
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);

                // Ensure parent directory exists
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Escritura atómica (mismo patrón que AppConfig.Save):
                // escribimos a un .tmp en la misma carpeta y luego
                // File.Replace, que es atómico en NTFS. Si el proceso
                // se corta a mitad de un File.WriteAllText clásico, el
                // manifest.json quedaría corrupto y la próxima carga
                // del job perdería todo el progreso. Con .tmp + Replace
                // o se ve la versión vieja o se ve la nueva, nunca un
                // archivo trunco. Importante: el manifest se graba una
                // vez por página procesada (alta frecuencia), por lo
                // que la ventana de race es real.
                string tmpPath = Path.Combine(
                    dir ?? ".",
                    $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(tmpPath, json);
                if (File.Exists(filePath))
                {
                    File.Replace(tmpPath, filePath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tmpPath, filePath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to save manifest to {filePath}: {ex.Message}");
            }
        }
    }

    public class JobFileManifest
    {
        public string SourceFilePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string NormalizedFileName { get; set; } = string.Empty;
        public string CopiedFilePath { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = "Spanish";
        public bool Completed { get; set; } = false;
        public List<JobPageManifest> Pages { get; set; } = new();
    }

    public class JobPageManifest
    {
        public int PageNumber { get; set; }
        public bool OcrCompleted { get; set; } = false;
        public string? OcrError { get; set; }
        public bool TranslationCompleted { get; set; } = false;
        public string? TranslationError { get; set; }
        public bool ReviewCompleted { get; set; } = false;
        public string? ReviewError { get; set; }
        public bool ConversionCompleted { get; set; } = false;
    }
}
