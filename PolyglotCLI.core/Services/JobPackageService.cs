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

                // El manifest exportado en otra máquina trae SourceFilePath
                // apuntando al árbol de jobs original (p.ej.
                // C:\Users\...\AppData\Roaming\PolyglotCLI\jobs\...\sources\doc.pdf
                // o D:\mis_docs\doc.pdf). Esas rutas no existen en el equipo
                // que importa y no tienen valor: el sistema siempre trabaja
                // sobre las copias depositadas en <jobDir>\sources\, que es
                // lo único que vino dentro del .zpg. Después del move al
                // directorio final reescribimos cada SourceFilePath a esa
                // copia local para que el orquestador (ReprocessPageAsync,
                // UpdatePageOcr, etc.) pueda abrir el archivo sin
                // depender de rutas del equipo de origen.

                effectiveJobId = ResolveTargetJobId(jobsRoot, originalJobId);

                string finalDir = Path.Combine(jobsRoot, effectiveJobId);
                if (Directory.Exists(finalDir))
                {
                    Directory.Delete(finalDir, recursive: true);
                }

                Directory.Move(extractedRoot, finalDir);
                extractedRoot = null;

                RewriteSourceFilePathsToLocalCopy(manifest, finalDir);

                // Persistir el manifest en su ubicación final. Reutilizamos
                // el objeto en memoria (que ya tiene SourceFilePath
                // reescrito y, si corresponde, JobId actualizado) en vez de
                // re-leer el JSON del paquete, porque ese re-read
                // descartaría las reescrituras de SourceFilePath.
                var finalManifestPath = Path.Combine(finalDir, "manifest.json");
                if (File.Exists(finalManifestPath))
                {
                    try
                    {
                        if (!string.Equals(manifest.JobId, effectiveJobId, StringComparison.Ordinal))
                        {
                            manifest.JobId = effectiveJobId;
                        }
                        manifest.LastUpdatedAt = DateTime.Now;
                        // JobManifest.Save ya implementa el patrón
                        // atómico (.tmp + File.Replace), no usamos
                        // File.WriteAllTextAsync directo acá.
                        manifest.Save(finalManifestPath);
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

        /// <summary>
        /// Reescribe cada <c>SourceFilePath</c> del manifest a la ruta
        /// absoluta de la copia depositada en <c>finalDir\sources\</c> del
        /// job recién importado. Esto desacopla el manifest del equipo
        /// donde se exportó: da igual que el SourceFilePath original
        /// apuntara a <c>D:\mis_docs\doc.pdf</c> en la máquina de origen
        /// o incluyera segmentos <c>..</c>; el sistema siempre abrirá el
        /// archivo que vino dentro del paquete.
        ///
        /// <para>El nombre destino se toma de <c>NormalizedFileName</c>
        /// (saneado a <c>[a-zA-Z0-9_\-\.]</c> al crear el job) con
        /// fallback a <c>OriginalFileName</c> y por último al basename
        /// del path original. Como ninguno de los tres puede contener
        /// separadores ni <c>..</c>, el rewrite nunca puede escapar del
        /// directorio <c>sources/</c> del job.</para>
        /// </summary>
        private static void RewriteSourceFilePathsToLocalCopy(JobManifest manifest, string finalDir)
        {
            if (manifest.Files == null) return;
            if (string.IsNullOrEmpty(finalDir)) return;

            string sourcesDir = Path.Combine(finalDir, "sources");
            foreach (var file in manifest.Files)
            {
                if (file == null) continue;

                string name = !string.IsNullOrEmpty(file.NormalizedFileName)
                    ? file.NormalizedFileName
                    : (!string.IsNullOrEmpty(file.OriginalFileName)
                        ? file.OriginalFileName
                        : Path.GetFileName(file.SourceFilePath ?? string.Empty));

                if (string.IsNullOrEmpty(name))
                {
                    // Sin nombre derivable: queda vacío. El re-proceso
                    // simplemente no tendrá archivo fuente (mismo efecto
                    // que si el original estuviera ausente).
                    file.SourceFilePath = string.Empty;
                    continue;
                }

                file.SourceFilePath = Path.Combine(sourcesDir, name);
            }
        }
    }
}
