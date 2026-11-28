using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PolyglotCLI.Localization;
using PolyglotCLI.Validation;

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

        /// <summary>
        /// Límites anti zip-bomb para la importación: tamaño total
        /// descomprimido y cantidad de entradas. Un job real queda muy
        /// por debajo (PNGs de páginas + JSON + fuentes).
        /// </summary>
        public const long MaxTotalUncompressedBytes = 4L * 1024 * 1024 * 1024;
        public const int MaxEntryCount = 20_000;

        /// <summary>
        /// Carpetas del job que NO se exportan (logs locales: rutas de la
        /// máquina, nombres de usuario, etc.).
        /// </summary>
        private static readonly string[] ExcludedExportDirectories = { "logs" };

        /// <summary>Nombre del snapshot de configuración del job.</summary>
        private const string JobConfigFileName = "config.json";

        public static void ExportJobPackage(string jobDir, Stream output)
        {
            if (string.IsNullOrWhiteSpace(jobDir))
                throw new ArgumentException(CoreText.Get("Core.JobDirectoryRequired", "The job folder is required."), nameof(jobDir));
            if (!Directory.Exists(jobDir))
                throw new DirectoryNotFoundException(CoreText.Format("Core.JobDirectoryNotFound", "Job folder not found: {0}", jobDir));

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
                AddDirectoryToArchive(archive, jobDir, rootName, isJobRoot: true);

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

        private static void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryPrefix, bool isJobRoot = false)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(filePath);
                var entry = archive.CreateEntry($"{entryPrefix}/{fileName}", CompressionLevel.Optimal);
                using var dst = entry.Open();

                if (isJobRoot && string.Equals(fileName, JobConfigFileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Defensa en profundidad: aunque los jobs nuevos ya
                    // guardan config.json sin secretos, los jobs creados
                    // por versiones anteriores pueden tener API keys.
                    byte[] sanitized = StripSecretsFromConfigJson(File.ReadAllText(filePath));
                    dst.Write(sanitized, 0, sanitized.Length);
                    continue;
                }

                using var src = File.OpenRead(filePath);
                src.CopyTo(dst, CompressionBufferSize);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                if (isJobRoot && Array.Exists(ExcludedExportDirectories, d => string.Equals(d, dirName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                AddDirectoryToArchive(archive, subDir, $"{entryPrefix}/{dirName}");
            }
        }

        /// <summary>
        /// Elimina ApiKey, ProviderApiKeys y ProviderConfigs[*].ApiKey de un
        /// config.json. Si el JSON no se puede parsear, se exporta un objeto
        /// vacío: preferimos perder ajustes a filtrar una key.
        /// </summary>
        internal static byte[] StripSecretsFromConfigJson(string json)
        {
            JsonObject root;
            try
            {
                root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }

            root.Remove(nameof(AppConfig.ApiKey));
            root.Remove(nameof(AppConfig.ProviderApiKeys));
            if (root[nameof(AppConfig.ProviderConfigs)] is JsonObject providerConfigs)
            {
                foreach (var kvp in providerConfigs)
                {
                    if (kvp.Value is JsonObject pc)
                    {
                        pc.Remove(nameof(ProviderConfig.ApiKey));
                    }
                }
            }

            return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
                // .NET 11 puede lanzar ArgumentOutOfRangeException (o IOException)
                // en lugar de InvalidDataException con archivos truncados/diminutos.
                catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
                {
                    throw new InvalidJobPackageException(
                        CoreText.Get("Core.PackageNotZip", "The file is not a valid ZIP archive or is corrupted."), ex);
                }

                using (archive)
                {
                    if (archive.Entries.Count == 0)
                    {
                        throw new InvalidJobPackageException(CoreText.Get("Core.PackageEmpty", "The ZIP file is empty."));
                    }

                    EnsureWithinSizeLimits(archive);

                    topLevel = DetectTopLevelPrefix(archive);
                    if (string.IsNullOrEmpty(topLevel))
                    {
                        var sample = string.Join(", ",
                            archive.Entries.Take(5).Select(e => e.FullName));
                        throw new InvalidJobPackageException(
                            CoreText.Format("Core.PackageNoRootFolder", "The file does not contain a recognizable job root folder. Make sure it was exported from the job history. Entries found (first 5): {0}", sample));
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
                            CoreText.Format("Core.PackageExtractFailed", "The file could not be extracted: {0}", ex.Message), ex);
                    }

                    extractedRoot = Path.Combine(stagingDir, topLevel);
                }

                var manifestPath = Path.Combine(stagingDir, topLevel, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    var found = Directory.Exists(extractedRoot)
                        ? string.Join(", ", Directory.GetFiles(extractedRoot).Select(Path.GetFileName))
                        : CoreText.Get("Core.PackageFolderNotFound", "(folder not found)");
                    throw new InvalidJobPackageException(
                        CoreText.Format("Core.PackageNoManifest", "The package was extracted but has no manifest.json at its root. Files found: {0}", found));
                }

                JobManifest? manifest;
                try
                {
                    var rawJson = File.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize<JobManifest>(rawJson);
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.JobId))
                    {
                        throw new InvalidJobPackageException(
                            CoreText.Get("Core.PackageManifestEmpty", "The package's manifest.json is empty or invalid."));
                    }
                }
                catch (JsonException jx)
                {
                    throw new InvalidJobPackageException(CoreText.Get("Core.PackageManifestInvalidJson", "The manifest.json is not valid JSON."), jx);
                }

                var originalJobId = SanitizeJobId(manifest.JobId);
                if (string.IsNullOrEmpty(originalJobId))
                {
                    throw new InvalidJobPackageException(
                        CoreText.Get("Core.PackageInvalidJobId", "The manifest.json has an invalid JobId after normalization."));
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

                // El OutputDirectory del manifest viene de otra máquina (o
                // de un paquete manipulado) y se usa al reanudar: lo
                // reseteamos a la carpeta de salida por defecto local.
                manifest.OutputDirectory = AppConfig.GetDefaultOutputDirectory();

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

        /// <summary>
        /// Rechaza paquetes con demasiadas entradas o cuyo tamaño
        /// descomprimido declarado supera <see cref="MaxTotalUncompressedBytes"/>
        /// (zip bomb) ANTES de extraer nada a disco.
        /// </summary>
        private static void EnsureWithinSizeLimits(ZipArchive archive)
        {
            if (archive.Entries.Count > MaxEntryCount)
            {
                throw new InvalidJobPackageException(
                    CoreText.Format("Core.PackageTooManyEntries", "The package has too many entries ({0}; maximum {1}).", archive.Entries.Count, MaxEntryCount));
            }

            long total = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length < 0)
                {
                    throw new InvalidJobPackageException(CoreText.Format("Core.PackageEntryInvalidSize", "The entry '{0}' declares an invalid size.", entry.FullName));
                }
                total += entry.Length;
                if (total > MaxTotalUncompressedBytes)
                {
                    throw new InvalidJobPackageException(
                        CoreText.Format("Core.PackageTooLarge", "The uncompressed package exceeds the maximum allowed size ({0} MB).", MaxTotalUncompressedBytes / (1024 * 1024)));
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
        /// <para>El nombre destino se toma de <c>NormalizedFileName</c> con
        /// fallback a <c>OriginalFileName</c> y por último al basename
        /// del path original. Como el manifest viene de afuera (no es
        /// confiable), cada candidato se reduce a su basename
        /// (<c>Path.GetFileName</c>) y se valida con
        /// <see cref="PathTraversalGuard"/>: si el resultado no queda
        /// estrictamente dentro de <c>sources/</c>, el path se deja vacío.</para>
        /// </summary>
        private static void RewriteSourceFilePathsToLocalCopy(JobManifest manifest, string finalDir)
        {
            if (manifest.Files == null) return;
            if (string.IsNullOrEmpty(finalDir)) return;

            string sourcesDir = Path.Combine(finalDir, "sources");
            foreach (var file in manifest.Files)
            {
                if (file == null) continue;

                string name = SafeBaseName(file.NormalizedFileName);
                if (string.IsNullOrEmpty(name)) name = SafeBaseName(file.OriginalFileName);
                if (string.IsNullOrEmpty(name)) name = SafeBaseName(file.SourceFilePath);

                if (string.IsNullOrEmpty(name)
                    || !PathTraversalGuard.TryResolveInside(sourcesDir, name, out var resolved)
                    || string.Equals(resolved, Path.GetFullPath(sourcesDir), StringComparison.OrdinalIgnoreCase))
                {
                    // Sin nombre derivable o nombre que escaparía de
                    // sources/: queda vacío. El re-proceso simplemente no
                    // tendrá archivo fuente (mismo efecto que si el
                    // original estuviera ausente).
                    AppLogger.Warn($"Import: nombre de archivo fuente inválido en el manifest ('{file.NormalizedFileName}'). Se deja sin ruta.");
                    file.SourceFilePath = string.Empty;
                    file.CopiedFilePath = string.Empty;
                    continue;
                }

                file.SourceFilePath = resolved;
                file.CopiedFilePath = resolved;
            }
        }

        /// <summary>
        /// Basename de un valor del manifest, aceptando separadores de
        /// Windows y Unix (un manifest creado en otra plataforma puede usar
        /// cualquiera). Devuelve vacío para "." / ".." o nombres con ':'
        /// (rutas con unidad o ADS).
        /// </summary>
        private static string SafeBaseName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Replace('\\', '/');
            int idx = normalized.LastIndexOf('/');
            string name = (idx >= 0 ? normalized.Substring(idx + 1) : normalized).Trim();
            name = Path.GetFileName(name);
            if (name == "." || name == ".." || name.Contains(':')) return string.Empty;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return string.Empty;
            return name;
        }
    }
}
