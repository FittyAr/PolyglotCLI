using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public class InvalidJobPackageException : Exception
    {
        public InvalidJobPackageException(string message) : base(message) { }
        public InvalidJobPackageException(string message, Exception inner) : base(message, inner) { }
    }

    public static class JobPackageService
    {
        public const string PackageExtension = ".zpg";
        public const string PackageMimeType = "application/zip";
        private const int CompressionBufferSize = 81920;

        public static void ExportJobPackage(string jobDir, Stream output)
        {
            if (string.IsNullOrWhiteSpace(jobDir))
                throw new ArgumentException("Job directory is required.", nameof(jobDir));
            if (!Directory.Exists(jobDir))
                throw new DirectoryNotFoundException($"Job directory not found: {jobDir}");

            var manifestPath = Path.Combine(jobDir, "manifest.json");
            bool isIncomplete = false;
            try
            {
                if (File.Exists(manifestPath))
                {
                    var manifest = JobManifest.Load(manifestPath);
                    if (manifest != null)
                    {
                        isIncomplete = !string.Equals(manifest.Status, "Completed", StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    isIncomplete = true;
                }
            }
            catch
            {
                isIncomplete = true;
            }

            string rootName = new DirectoryInfo(jobDir).Name;

            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddDirectoryToArchive(archive, jobDir, rootName);

                if (isIncomplete)
                {
                    var noteEntry = archive.CreateEntry($"{rootName}/PACKAGE_NOTES.txt", CompressionLevel.Optimal);
                    using var writer = new StreamWriter(noteEntry.Open());
                    writer.WriteLine("Exportación Parcial");
                    writer.WriteLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("El trabajo no estaba en estado Completed al momento de la exportación.");
                    writer.WriteLine("Algunos archivos de 'data/' pueden estar parcialmente escritos.");
                }
            }
        }

        private static void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryPrefix)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(filePath);
                var entry = archive.CreateEntry($"{entryPrefix}/{fileName}", CompressionLevel.Optimal);
                using var src = File.OpenRead(filePath);
                using var dst = entry.Open();
                src.CopyTo(dst, CompressionBufferSize);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                AddDirectoryToArchive(archive, subDir, $"{entryPrefix}/{dirName}");
            }
        }

        public static async Task<string> ImportJobPackageAsync(Stream input, string jobsRoot)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(jobsRoot)) throw new ArgumentException("Jobs root is required.", nameof(jobsRoot));

            if (!Directory.Exists(jobsRoot))
            {
                Directory.CreateDirectory(jobsRoot);
            }

            string stagingDir = Path.Combine(Path.GetTempPath(), $"polyglot-cli-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDir);

            string? extractedRoot = null;
            string? effectiveJobId = null;
            string? topLevel = null;

            try
            {
                ZipArchive archive;
                try
                {
                    archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
                }
                catch (InvalidDataException ide)
                {
                    throw new InvalidJobPackageException(
                        "El archivo no es un ZIP válido o está corrupto.", ide);
                }

                using (archive)
                {
                    if (archive.Entries.Count == 0)
                    {
                        throw new InvalidJobPackageException("El archivo zip está vacío.");
                    }

                    topLevel = DetectTopLevelPrefix(archive);
                    if (string.IsNullOrEmpty(topLevel))
                    {
                        var sample = string.Join(", ",
                            archive.Entries.Take(5).Select(e => e.FullName));
                        throw new InvalidJobPackageException(
                            $"El archivo no contiene una carpeta raíz de trabajo reconocible. " +
                            $"Asegúrate de exportar el archivo desde el Historial de Trabajos. " +
                            $"Entradas detectadas (primeras 5): {sample}");
                    }

                    // Extraer directamente al stagingDir: las entradas del tipo
                    // "{topLevel}/manifest.json" se materializan en
                    // "{stagingDir}/{topLevel}/manifest.json", que es justo donde
                    // buscaremos el manifiesto debajo.
                    try
                    {
                        archive.ExtractToDirectory(stagingDir, overwriteFiles: true);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidJobPackageException(
                            $"No se pudo extraer el archivo: {ex.Message}", ex);
                    }

                    extractedRoot = Path.Combine(stagingDir, topLevel);
                }

                var manifestPath = Path.Combine(stagingDir, topLevel, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    var found = Directory.Exists(extractedRoot)
                        ? string.Join(", ", Directory.GetFiles(extractedRoot).Select(Path.GetFileName))
                        : "(directorio no encontrado)";
                    throw new InvalidJobPackageException(
                        $"El paquete fue extraído pero no contiene un manifest.json en su raíz. " +
                        $"Archivos encontrados: {found}");
                }

                JobManifest? manifest;
                try
                {
                    var rawJson = File.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize<JobManifest>(rawJson);
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.JobId))
                    {
                        throw new InvalidJobPackageException(
                            "El manifest.json del paquete está vacío o es inválido.");
                    }
                }
                catch (JsonException jx)
                {
                    throw new InvalidJobPackageException("El manifest.json no es un JSON válido.", jx);
                }

                var originalJobId = SanitizeJobId(manifest.JobId);
                if (string.IsNullOrEmpty(originalJobId))
                {
                    throw new InvalidJobPackageException(
                        "El manifest.json tiene un JobId inválido tras la normalización.");
                }

                effectiveJobId = ResolveTargetJobId(jobsRoot, originalJobId);

                string finalDir = Path.Combine(jobsRoot, effectiveJobId);
                if (Directory.Exists(finalDir))
                {
                    Directory.Delete(finalDir, recursive: true);
                }

                Directory.Move(extractedRoot, finalDir);
                extractedRoot = null;

                var finalManifestPath = Path.Combine(finalDir, "manifest.json");
                if (File.Exists(finalManifestPath))
                {
                    try
                    {
                        var existingJson = await File.ReadAllTextAsync(finalManifestPath);
                        var existing = JsonSerializer.Deserialize<JobManifest>(existingJson);
                        if (existing != null)
                        {
                            existing.JobId = effectiveJobId;
                            existing.LastUpdatedAt = DateTime.Now;
                            await File.WriteAllTextAsync(
                                finalManifestPath,
                                JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
                        }
                    }
                    catch (Exception fixEx)
                    {
                        AppLogger.Warn($"No se pudo reescribir JobId en manifest después de renombrar: {fixEx.Message}");
                    }
                }

                AppLogger.Info($"Imported job package as '{effectiveJobId}'");
                return effectiveJobId!;
            }
            finally
            {
                try
                {
                    if (extractedRoot != null && Directory.Exists(extractedRoot))
                    {
                        Directory.Delete(extractedRoot, recursive: true);
                    }
                    if (Directory.Exists(stagingDir))
                    {
                        Directory.Delete(stagingDir, recursive: true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    AppLogger.Warn($"Failed to cleanup staging dir {stagingDir}: {cleanupEx.Message}");
                }
            }
        }

        private static string? DetectTopLevelPrefix(ZipArchive archive)
        {
            string? rootCandidate = null;
            int entryCount = 0;
            foreach (var entry in archive.Entries)
            {
                entryCount++;
                var name = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (string.IsNullOrEmpty(name)) continue;
                var firstSegment = name.Split('/')[0];
                if (string.IsNullOrEmpty(firstSegment)) continue;

                if (rootCandidate == null) rootCandidate = firstSegment;
                else if (!string.Equals(rootCandidate, firstSegment, StringComparison.Ordinal))
                {
                    return name.Contains('/') ? firstSegment : null;
                }
            }

            if (entryCount == 0) return null;
            return rootCandidate;
        }

        private static string SanitizeJobId(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return string.Empty;
            return Regex.Replace(jobId.Trim(), @"[^a-zA-Z0-9_\-\.]", "");
        }

        private static string ResolveTargetJobId(string jobsRoot, string originalJobId)
        {
            var candidate = originalJobId;
            var targetPath = Path.Combine(jobsRoot, candidate);
            if (!Directory.Exists(targetPath))
            {
                return candidate;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string suffixed = $"{candidate}_imported_{stamp}";
            targetPath = Path.Combine(jobsRoot, suffixed);

            int counter = 1;
            while (Directory.Exists(targetPath))
            {
                suffixed = $"{candidate}_imported_{stamp}_{counter}";
                targetPath = Path.Combine(jobsRoot, suffixed);
                counter++;
            }
            return suffixed;
        }
    }
}
