using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolyglotCLI.Validation;

namespace PolyglotCLI
{
    public static class JobManifestService
    {
        public static void InitializeJobDirectory(string jobDir, AppConfig config)
        {
            if (!Directory.Exists(jobDir))
            {
                Directory.CreateDirectory(jobDir);
            }

            // Copia de los ajustes del job SIN secretos: la carpeta del job
            // se exporta en paquetes .zpg y no debe contener API keys.
            // Al reanudar sólo se leen los ajustes de ajuste fino
            // (timeouts, temperaturas, chunking...), nunca las keys.
            //
            // Si ya existe (reanudación) NO se pisa: contiene los ajustes
            // con los que se creó el job, que LoadOrInitializeManifest
            // vuelve a aplicar. Sólo se le quitan secretos que hayan
            // quedado de versiones anteriores.
            try
            {
                string jobConfigPath = Path.Combine(jobDir, "config.json");
                if (File.Exists(jobConfigPath))
                {
                    File.WriteAllBytes(jobConfigPath, JobPackageService.StripSecretsFromConfigJson(File.ReadAllText(jobConfigPath)));
                }
                else
                {
                    config.CloneWithoutSecrets().Save(jobConfigPath);
                }
            }
            catch (Exception cfgEx)
            {
                AppLogger.Warn($"Failed to save config.json copy to job directory: {cfgEx.Message}");
            }
        }

        /// <summary>
        /// Carga el manifest de un job a reanudar o crea uno nuevo.
        /// IMPORTANTE: al reanudar, los ajustes del job se escriben sobre
        /// <paramref name="config"/>; el caller debe pasar una copia
        /// por-ejecución (<see cref="AppConfig.Clone"/>), nunca el
        /// singleton compartido de la app.
        /// </summary>
        public static JobManifest LoadOrInitializeManifest(
            string jobDir, 
            CommandLineOptions options, 
            AppConfig config, 
            string manifestPath)
        {
            JobManifest currentManifest;
            if (!string.IsNullOrEmpty(options.ResumeJobId) && File.Exists(manifestPath))
            {
                currentManifest = JobManifest.Load(manifestPath);

                // Validación defensiva (PR 2). El manifest es
                // data user-controlled (el usuario puede editarlo
                // a mano). Logueamos warnings por cada campo
                // sospechoso pero NO rechazamos la carga: el
                // usuario probablemente quiere reanudar el job
                // aunque tenga campos "raros".
                ValidateManifestAndLog("JobManifestService.LoadOrInitializeManifest", currentManifest);
                string jobConfigPath = Path.Combine(jobDir, "config.json");
                if (File.Exists(jobConfigPath))
                {
                    try
                    {
                        var jobConfig = AppConfig.Load(jobConfigPath);
                        config.TranslationTimeoutSeconds = jobConfig.TranslationTimeoutSeconds;
                        config.OcrTimeoutSeconds = jobConfig.OcrTimeoutSeconds;
                        config.ReviewTimeoutSeconds = jobConfig.ReviewTimeoutSeconds;
                        config.Temperature = jobConfig.Temperature;
                        config.OcrTemperature = jobConfig.OcrTemperature;
                        config.ReviewTemperature = jobConfig.ReviewTemperature;
                        config.MaxCharactersPerChunk = jobConfig.MaxCharactersPerChunk;
                        config.ChunkOverlapCharacters = jobConfig.ChunkOverlapCharacters;
                        config.PreserveFormat = jobConfig.PreserveFormat;
                        config.EnableReview = jobConfig.EnableReview;
                        config.ReviewModel = jobConfig.ReviewModel;
                        AppLogger.Info($"Loaded custom configuration settings from job copy of config.json.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to load config.json copy from job directory: {ex.Message}");
                    }
                }

                // Override options with manifest values so the run is consistent!
                options.Mode = currentManifest.Mode;
                options.TargetLanguage = currentManifest.TargetLanguage;
                options.OutputDirectory = currentManifest.OutputDirectory;
                options.PageRange = currentManifest.PageRange;
                options.ModelName = currentManifest.ModelName;
                options.VisionModelName = currentManifest.VisionModelName;
                options.AdditionalPrompt = currentManifest.AdditionalPrompt;
                options.Transcribe = currentManifest.Transcribe;
                options.Translate = currentManifest.Translate;
                options.Verify = currentManifest.Verify;
                options.GenerateDoc = currentManifest.GenerateDoc;
                options.SelectedFormat = currentManifest.SelectedFormat;
                
                // Rebuild files list and targets
                options.Files.Clear();
                options.DocumentTargets.Clear();
                foreach (var fileM in currentManifest.Files)
                {
                    options.Files.Add(fileM.SourceFilePath);
                    options.DocumentTargets.Add(new DocumentTarget
                    {
                        FilePath = fileM.SourceFilePath,
                        Mode = currentManifest.Mode,
                        PageRange = currentManifest.PageRange,
                        // Mismo chunking que la ejecución original: si no,
                        // los chunks no coinciden y se pierde el progreso.
                        MaxCharactersPerChunk = config.MaxCharactersPerChunk,
                        ChunkOverlapCharacters = config.ChunkOverlapCharacters
                    });
                }
                
                AppLogger.Info($"Resuming past job: '{options.ResumeJobId}'");
            }
            else
            {
                currentManifest = new JobManifest
                {
                    JobId = Path.GetFileName(jobDir),
                    CreatedAt = DateTime.Now,
                    LastUpdatedAt = DateTime.Now,
                    Status = "InProgress",
                    TargetLanguage = options.TargetLanguage,
                    Mode = options.Mode,
                    OutputDirectory = options.OutputDirectory,
                    PageRange = options.PageRange,
                    ModelName = options.ModelName,
                    VisionModelName = options.VisionModelName,
                    AdditionalPrompt = options.AdditionalPrompt,
                    Transcribe = options.Transcribe,
                    Translate = options.Translate,
                    Verify = options.Verify,
                    GenerateDoc = options.GenerateDoc,
                    SelectedFormat = options.SelectedFormat
                };
                
                // Populate files in manifest
                string sourcesDir = Path.Combine(jobDir, "sources");
                if (!Directory.Exists(sourcesDir))
                {
                    Directory.CreateDirectory(sourcesDir);
                }

                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var target in options.DocumentTargets)
                {
                    string originalFileName = Path.GetFileName(target.FilePath);
                    string normalizedFileName = BuildUniqueNormalizedFileName(originalFileName, usedNames);
                    string copiedFilePath = Path.Combine(sourcesDir, normalizedFileName);
                    string effectivePath = target.FilePath;

                    try
                    {
                        if (!string.Equals(Path.GetFullPath(target.FilePath), Path.GetFullPath(copiedFilePath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(target.FilePath, copiedFilePath, true);
                        }
                        // Sólo se trabaja sobre la copia si realmente existe.
                        effectivePath = copiedFilePath;
                    }
                    catch (Exception ex)
                    {
                        // Si la copia falla seguimos con el archivo original
                        // en vez de apuntar a una copia inexistente.
                        AppLogger.Error($"Failed to copy {originalFileName} to sources directory: {ex.Message}. Se usará el archivo original.");
                    }

                    target.FilePath = effectivePath;

                    currentManifest.Files.Add(new JobFileManifest
                    {
                        SourceFilePath = effectivePath,
                        OriginalFileName = originalFileName,
                        NormalizedFileName = normalizedFileName,
                        CopiedFilePath = effectivePath == copiedFilePath ? copiedFilePath : string.Empty,
                        TargetLanguage = options.TargetLanguage
                    });
                }
                
                currentManifest.Save(manifestPath);
            }

            return currentManifest;
        }

        private static readonly Regex UnsafeFileNameChars = new(@"[^\p{L}\p{N}_\-\.]", RegexOptions.Compiled);

        /// <summary>
        /// Normaliza un nombre de archivo para la carpeta sources/ del job:
        /// conserva letras y dígitos Unicode (acentos, CJK...), '_', '-' y
        /// '.'; si queda vacío usa un GUID; y si colisiona con otro archivo
        /// del mismo job agrega un sufijo numérico ("doc_2.pdf").
        /// </summary>
        public static string BuildUniqueNormalizedFileName(string originalFileName, ISet<string> usedNames)
        {
            string extension = UnsafeFileNameChars.Replace(Path.GetExtension(originalFileName ?? string.Empty), string.Empty);
            string stem = UnsafeFileNameChars.Replace(Path.GetFileNameWithoutExtension(originalFileName ?? string.Empty), string.Empty).Trim('.');
            if (string.IsNullOrEmpty(stem))
            {
                stem = Guid.NewGuid().ToString("N");
            }

            string candidate = stem + extension;
            int counter = 2;
            while (usedNames.Contains(candidate))
            {
                candidate = $"{stem}_{counter}{extension}";
                counter++;
            }
            usedNames.Add(candidate);
            return candidate;
        }

        public static void UpdatePageOcr(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.OcrCompleted = true;
                page.OcrError = null;
            }
            else
            {
                page.OcrCompleted = false;
                page.OcrError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdatePageTranslation(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.TranslationCompleted = true;
                page.TranslationError = null;
            }
            else
            {
                page.TranslationCompleted = false;
                page.TranslationError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdatePageReview(JobManifest manifest, string manifestPath, string filePath, int pageNum, bool success, string? error)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file == null)
            {
                file = new JobFileManifest
                {
                    SourceFilePath = filePath,
                    OriginalFileName = Path.GetFileName(filePath),
                    NormalizedFileName = Path.GetFileName(filePath),
                    TargetLanguage = manifest.TargetLanguage
                };
                manifest.Files.Add(file);
            }

            var page = file.Pages.Find(p => p.PageNumber == pageNum);
            if (page == null)
            {
                page = new JobPageManifest { PageNumber = pageNum };
                file.Pages.Add(page);
            }

            if (success)
            {
                page.ReviewCompleted = true;
                page.ReviewError = null;
            }
            else
            {
                page.ReviewCompleted = false;
                page.ReviewError = error;
            }

            manifest.Save(manifestPath);
        }

        public static void UpdateFileConversion(JobManifest manifest, string manifestPath, string filePath, bool success)
        {
            var file = manifest.Files.Find(f => f.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                foreach (var page in file.Pages)
                {
                    page.ConversionCompleted = success;
                }
                manifest.Save(manifestPath);
            }
        }

        public static void SavePageStatesToJson(List<PageProcessState> pageStates, string dataJsonPath)
        {
            string? tmpPath = null;
            try
            {
                string? dir = Path.GetDirectoryName(dataJsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dataList = PageStateMapper.ToPageData(pageStates);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(dataList, options);
                // Escritura atómica (mismo patrón que AppConfig.Save y
                // JobManifest.Save): un .tmp + File.Replace en lugar de
                // un File.WriteAllText directo. La data del job
                // (TranslatedText, OriginalText por página) se graba
                // una vez por página, así que la ventana de corrupción
                // por corte a mitad de write es alta si no es atómico.
                string tmpDir = string.IsNullOrEmpty(dir) ? "." : dir;
                tmpPath = Path.Combine(
                    tmpDir,
                    $".{Path.GetFileName(dataJsonPath)}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(tmpPath, json);
                if (File.Exists(dataJsonPath))
                {
                    File.Replace(tmpPath, dataJsonPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tmpPath, dataJsonPath);
                }
                tmpPath = null;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to save JSON state to {dataJsonPath}: {ex.Message}");
            }
            finally
            {
                if (tmpPath != null)
                {
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); }
                    catch { /* best effort */ }
                }
            }
        }

        /// <summary>
        /// Resultado de resolver la ubicación real de la carpeta de un
        /// trabajo. Se usa para detectar inconsistencias entre el
        /// JobId del manifest y el nombre del directorio en disco
        /// (por ejemplo, cuando el usuario renombra la carpeta a
        /// mano: '20260710_223809' → '20260710_223809_old').
        ///
        /// - <see cref="IsConsistent"/> es true si la carpeta está
        ///   exactamente donde se espera.
        /// - <see cref="ActualPath"/> tiene el path real (puede ser
        ///   null si no se encontró).
        /// - <see cref="SimilarDirectories"/> lista otras carpetas en
        ///   el jobs root que matchean parcialmente con el JobId
        ///   (sirve para que la UI le sugiera al usuario dónde puede
        ///   estar la carpeta renombrada).
        /// </summary>
        public class JobDirectoryResolution
        {
            public string JobId { get; init; } = string.Empty;
            public string ExpectedPath { get; init; } = string.Empty;
            public string? ActualPath { get; set; }
            public List<string> SimilarDirectories { get; set; } = new();

            public bool IsConsistent => ActualPath != null;
        }

        /// <summary>
        /// Valida que un JobId sea un nombre de carpeta legítimo y
        /// seguro. El JobId se guarda en el manifest.json y se
        /// concatena a un path de filesystem en múltiples lugares
        /// (cargar, borrar, exportar, reanudar, etc.). Si el manifest
        /// fue editado a mano o viene de una fuente no confiable, el
        /// JobId podría contener path traversal (ej: '..\\..\\foo')
        /// o caracteres maliciosos. Esta función es la primera línea
        /// de defensa: si devuelve false, ningún consumidor debería
        /// tocar el filesystem con ese JobId.
        /// </summary>
        public static bool IsValidJobId(string? jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return false;

            // No permitir '..' (path traversal).
            if (jobId.Contains("..")) return false;

            // No permitir separadores de path: el JobId es solo el
            // nombre de la carpeta, no un sub-path. (Esto es lo que
            // lo protege de "../foo", "foo/bar", "foo\bar", etc.)
            if (jobId.Contains('/') || jobId.Contains('\\')) return false;

            // No permitir NUL ni control chars.
            foreach (char c in jobId)
            {
                if (c < 0x20) return false;
            }

            // Límite de longitud razonable.
            if (jobId.Length > 200) return false;

            return true;
        }

        /// <summary>
        /// Resuelve la ubicación real de la carpeta de un trabajo y
        /// reporta si hay inconsistencia entre el JobId y el nombre
        /// del directorio. Usar este método ANTES de cualquier
        /// operación que toque archivos del trabajo (ver detalles,
        /// exportar, reanudar, analizar, borrar) para que la UI
        /// pueda mostrarle al usuario el problema en vez de fallar
        /// con un error críptico o quedarse colgada.
        ///
        /// Si el JobId no es válido (path traversal, caracteres
        /// raros), devuelve una resolución inconsistente con
        /// ActualPath=null para que la UI aborte la operación en
        /// vez de tocar el filesystem.
        /// </summary>
        public static JobDirectoryResolution TryResolveJobDirectory(string jobId)
        {
            var result = new JobDirectoryResolution
            {
                JobId = jobId,
                ExpectedPath = Path.Combine(TranslationOrchestrator.GetJobsDirectory(), jobId)
            };

            if (!IsValidJobId(jobId))
            {
                AppLogger.Warn(
                    $"TryResolveJobDirectory: JobId inválido '{jobId}'. " +
                    $"Rechazado (posible path traversal o manifest corrupto).");
                return result; // ActualPath = null → IsConsistent = false
            }

            if (Directory.Exists(result.ExpectedPath))
            {
                result.ActualPath = result.ExpectedPath;
            }
            else
            {
                AppLogger.Warn(
                    $"TryResolveJobDirectory: directorio no encontrado para JobId='{jobId}'. " +
                    $"Path esperado: {result.ExpectedPath}. " +
                    $"Posible rename manual o move externo. " +
                    $"Todas las acciones sobre este job (ver, exportar, reanudar, borrar) van a fallar.");

                // Buscar candidatos que matcheen parcialmente.
                result.SimilarDirectories = FindSimilarJobDirectories(jobId);
            }

            return result;
        }

        /// <summary>
        /// Busca carpetas en el jobs root cuyo nombre matchea
        /// parcialmente con el JobId. Útil para diagnosticar
        /// renames manuales (sufijos como _old, _backup, _copy, etc).
        /// Excluye el match exacto (que es el que ya sabemos que
        /// no existe).
        /// </summary>
        public static List<string> FindSimilarJobDirectories(string jobId)
        {
            var similar = new List<string>();
            try
            {
                string jobsDir = TranslationOrchestrator.GetJobsDirectory();
                if (!Directory.Exists(jobsDir)) return similar;

                foreach (var dir in Directory.GetDirectories(jobsDir))
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, jobId, StringComparison.OrdinalIgnoreCase))
                        continue; // Excluir el match exacto (no existe)

                    // Matchear si empieza con jobId o lo contiene
                    // (cubre _old, _backup, _copy, (1), .bak, etc.).
                    if (name.StartsWith(jobId, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(jobId, StringComparison.OrdinalIgnoreCase))
                    {
                        similar.Add(dir);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"FindSimilarJobDirectories: error: {ex.Message}");
            }
            return similar;
        }

        public static List<JobManifest> LoadPastJobs()
        {
            var pastJobs = new List<JobManifest>();
            string jobsDir = TranslationOrchestrator.GetJobsDirectory();
            if (!Directory.Exists(jobsDir))
            {
                return pastJobs;
            }

            try
            {
                var dirs = Directory.GetDirectories(jobsDir);
                foreach (var dir in dirs)
                {
                    string manifestPath = Path.Combine(dir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = JobManifest.Load(manifestPath);
                        if (manifest != null && !string.IsNullOrEmpty(manifest.JobId))
                        {
                            // Defensa contra manifests manipulados:
                            // un JobId con path traversal ('..\\..\\foo')
                            // o caracteres raros podría hacer que el
                            // código escape del directorio de jobs al
                            // construir paths. Si no pasa la validación,
                            // descartamos el manifest y seguimos.
                            if (!IsValidJobId(manifest.JobId))
                            {
                                AppLogger.Warn(
                                    $"LoadPastJobs: manifest con JobId inválido '{manifest.JobId}' " +
                                    $"en '{dir}'. Ignorando (posible path traversal o manifest corrupto).");
                                continue;
                            }

                            // Validación de consistencia: el JobId del
                            // manifest debería matchear con el nombre
                            // del directorio en disco. Si no coincide
                            // es probable que el usuario haya
                            // renombrado la carpeta a mano
                            // (ej: '20260710_223809' →
                            // '20260710_223809_old'). En ese caso el
                            // borrado desde la UI va a fallar porque
                            // busca por el JobId viejo. Logueamos el
                            // warning para diagnóstico y devolvemos
                            // el manifest igual (la UI es la que tiene
                            // que mostrarle al usuario el problema y
                            // dejarlo decidir qué hacer).
                            string dirName = Path.GetFileName(dir);
                            if (!string.Equals(manifest.JobId, dirName, StringComparison.OrdinalIgnoreCase))
                            {
                                AppLogger.Warn(
                                    $"Inconsistencia manifest↔disco: el manifest en '{dir}' " +
                                    $"tiene JobId='{manifest.JobId}' pero la carpeta se llama " +
 $"'{dirName}'. Probable rename manual. El borrado por JobId va a fallar hasta que " +
                                    $"se renombre la carpeta de vuelta o se actualice el manifest.");
                            }

                            pastJobs.Add(manifest);
                        }
                    }
                }
                
                // Sort jobs descending by JobId (newest first)
                pastJobs.Sort((a, b) => string.Compare(b.JobId, a.JobId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Error scanning past jobs: {ex.Message}");
            }
            return pastJobs;
        }

        public static List<string> GetJobDataFiles(string jobDir)
        {
            var files = new List<string>();
            string dataDir = Path.Combine(jobDir, "data");
            if (Directory.Exists(dataDir))
            {
                files.AddRange(Directory.GetFiles(dataDir, "*_data.json"));
            }
            foreach (var file in Directory.GetFiles(jobDir, "*_data.json"))
            {
                if (!files.Contains(file)) files.Add(file);
            }
            return files;
        }

        public static List<DocumentPageData> GetJobDataPages(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<DocumentPageData>>(json) ?? new List<DocumentPageData>();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Failed to load job data pages from {filePath}: {ex.Message}");
                }
            }
            return new List<DocumentPageData>();
        }

        public static string BuildErrorSummary(JobManifest manifest)
        {
            var sbErr = new System.Text.StringBuilder();
            foreach (var f in manifest.Files)
            {
                foreach (var p in f.Pages)
                {
                    if (!p.OcrCompleted && !string.IsNullOrEmpty(p.OcrError))
                        sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: OCR, Error: {p.OcrError}");
                    if (!p.TranslationCompleted && !string.IsNullOrEmpty(p.TranslationError))
                        sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: Translation, Error: {p.TranslationError}");
                }
            }
            return sbErr.ToString();
        }

        /// <summary>
        /// Validación defensiva de un <see cref="JobManifest"/>
        /// (PR 2 del plan). Recorre los campos user-controlled
        /// y loguea warnings. NO rechaza la carga: mantener
        /// retrocompat. El caller decide qué hacer.
        /// </summary>
        internal static void ValidateManifestAndLog(string context, JobManifest? manifest)
        {
            if (manifest == null) return;

            // ── Paths ──
            var pathResult = FileSystemPathValidator.SanitizeDirectoryPath(manifest.OutputDirectory);
            if (!pathResult.IsValid)
            {
                AppLogger.Warn(
                    $"{context}: JobManifest.OutputDirectory inválida: " +
                    $"{pathResult.FirstError} (valor: '{manifest.OutputDirectory}').");
            }

            // ── Model names ──
            if (!string.IsNullOrEmpty(manifest.ModelName))
            {
                var modelResult = ModelNameValidator.SanitizeModelName(manifest.ModelName);
                if (!modelResult.IsValid)
                    AppLogger.Warn($"{context}: JobManifest.ModelName inválido: {modelResult.FirstError}");
            }
            if (!string.IsNullOrEmpty(manifest.VisionModelName))
            {
                var modelResult = ModelNameValidator.SanitizeModelName(manifest.VisionModelName);
                if (!modelResult.IsValid)
                    AppLogger.Warn($"{context}: JobManifest.VisionModelName inválido: {modelResult.FirstError}");
            }

            // ── Prompts ──
            if (!string.IsNullOrEmpty(manifest.AdditionalPrompt))
            {
                var promptResult = PromptValidator.SanitizePrompt(manifest.AdditionalPrompt);
                if (!promptResult.IsValid)
                {
                    AppLogger.Warn(
                        $"{context}: JobManifest.AdditionalPrompt inválido: " +
                        $"{promptResult.FirstError}. Longitud: {manifest.AdditionalPrompt.Length}.");
                }
            }

            // ── File paths dentro del manifest ──
            // Cada archivo del job tiene su propio path que
            // va a File.Copy / File.ReadAllText. Si está
            // contaminado (path traversal), el code path que
            // use ese file va a tener problemas.
            foreach (var fileM in manifest.Files)
            {
                if (string.IsNullOrEmpty(fileM.SourceFilePath)) continue;

                var fileResult = FileSystemPathValidator.SanitizeFileName(
                    Path.GetFileName(fileM.SourceFilePath));
                if (!fileResult.IsValid)
                {
                    AppLogger.Warn(
                        $"{context}: JobManifest.Files[].SourceFilePath tiene nombre inválido: " +
                        $"{fileResult.FirstError} (path: '{fileM.SourceFilePath}').");
                }

                // Path completo: chequea traversal
                if (FileSystemPathValidator.ContainsPathTraversal(fileM.SourceFilePath))
                {
                    AppLogger.Warn(
                        $"{context}: JobManifest.Files[].SourceFilePath contiene path traversal: " +
                        $"'{fileM.SourceFilePath}'. Posible manifest malicioso o corrupto.");
                }
            }
        }
    }
}
