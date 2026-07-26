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

            // Save config copy
            try
            {
                config.Save(Path.Combine(jobDir, "config.json"));
            }
            catch (Exception cfgEx)
            {
                AppLogger.Warn($"Failed to save config.json copy to job directory: {cfgEx.Message}");
            }
        }

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
                        PageRange = currentManifest.PageRange
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

                foreach (var target in options.DocumentTargets)
                {
                    string originalFileName = Path.GetFileName(target.FilePath);
                    string normalizedFileName = Regex.Replace(originalFileName, @"[^a-zA-Z0-9_\-\.]", "");
                    string copiedFilePath = Path.Combine(sourcesDir, normalizedFileName);
                    
                    try
                    {
                        if (target.FilePath != copiedFilePath)
                            File.Copy(target.FilePath, copiedFilePath, true);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to copy {originalFileName} to sources directory: {ex.Message}");
                    }

                    target.FilePath = copiedFilePath;

                    currentManifest.Files.Add(new JobFileManifest
                    {
                        SourceFilePath = copiedFilePath,
                        OriginalFileName = originalFileName,
                        NormalizedFileName = normalizedFileName,
                        CopiedFilePath = copiedFilePath,
                        TargetLanguage = options.TargetLanguage
                    });
                }
                
                currentManifest.Save(manifestPath);
            }

            return currentManifest;
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
            try
            {
                string? dir = Path.GetDirectoryName(dataJsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dataList = new List<DocumentPageData>();
                foreach (var state in pageStates)
                {
                    dataList.Add(new DocumentPageData
                    {
                        PageNumber = state.PageNumber,
                        OriginalText = state.OcrText,
                        TranslatedText = state.TranslatedText,
                        ReviewedText = state.ReviewedText,
                        IsOcrSuccessful = !state.OcrFailed,
                        IsTranslationSuccessful = !state.TranslationFailed,
                        OcrErrorMessage = state.OcrErrorMessage,
                        TranslationErrorMessage = state.TranslationErrorMessage
                    });
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(dataList, options);
                // Escritura atómica (mismo patrón que AppConfig.Save y
                // JobManifest.Save): un .tmp + File.Replace en lugar de
                // un File.WriteAllText directo. La data del job
                // (TranslatedText, OriginalText por página) se graba
                // una vez por página, así que la ventana de corrupción
                // por corte a mitad de write es alta si no es atómico.
                string tmpDir = string.IsNullOrEmpty(dir) ? "." : dir;
                string tmpPath = Path.Combine(
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
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to save JSON state to {dataJsonPath}: {ex.Message}");
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
