using System;
using System.IO;
using System.Text.Json;

namespace PolyglotCLI
{
    public class ProviderConfig
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public bool IsTested { get; set; } = false;
        public List<string> AvailableModels { get; set; } = new List<string>();
    }

    public class AppConfig
    {
        public string Provider { get; set; } = "LmStudio";
        public string OcrProvider { get; set; } = "LmStudio";
        public string TranslationProvider { get; set; } = "LmStudio";
        public string ReviewProvider { get; set; } = "LmStudio";

        public string ApiUrl { get; set; } = "http://172.22.144.1:1234/v1";
        public string? ApiKey { get; set; }
        public Dictionary<string, string> ProviderApiKeys { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, ProviderConfig> ProviderConfigs { get; set; } = new Dictionary<string, ProviderConfig>();

        public ProviderConfig GetProviderConfig(string? providerStr = null)
        {
            string provider = providerStr ?? Provider;
            if (!string.IsNullOrWhiteSpace(provider) && ProviderConfigs.TryGetValue(provider, out var existingConfig))
            {
                if (provider.Equals(Provider, StringComparison.OrdinalIgnoreCase))
                {
                    existingConfig.ApiUrl = ApiUrl;
                    existingConfig.ApiKey = ApiKey;
                }
                return existingConfig;
            }

            var pEnum = LlmProviderHelper.ParseProvider(provider);
            var cfg = new ProviderConfig
            {
                ApiUrl = provider.Equals(Provider, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ApiUrl)
                    ? ApiUrl
                    : LlmProviderHelper.GetDefaultApiUrl(pEnum),
                ApiKey = GetApiKeyForProvider(provider),
                IsTested = false
            };
            return cfg;
        }

        public void SaveTestedProvider(string providerStr, string apiUrl, string? apiKey, List<string> models)
        {
            if (string.IsNullOrWhiteSpace(providerStr)) return;

            string normalizedProvider = providerStr.Trim();
            SetApiKeyForProvider(normalizedProvider, apiKey);

            if (!ProviderConfigs.TryGetValue(normalizedProvider, out var pCfg))
            {
                pCfg = new ProviderConfig();
                ProviderConfigs[normalizedProvider] = pCfg;
            }

            pCfg.ApiUrl = apiUrl;
            pCfg.ApiKey = apiKey;
            pCfg.IsTested = true;
            pCfg.AvailableModels = models ?? new List<string>();

            if (normalizedProvider.Equals(Provider, StringComparison.OrdinalIgnoreCase))
            {
                ApiUrl = apiUrl;
            }

            Save();
        }

        public List<string> GetModelsForProvider(string? providerStr)
        {
            string provider = providerStr ?? Provider;
            if (!string.IsNullOrWhiteSpace(provider) && ProviderConfigs.TryGetValue(provider, out var pCfg) && pCfg.AvailableModels.Count > 0)
            {
                return pCfg.AvailableModels;
            }

            return new List<string>();
        }

        public List<string> GetTestedProviders()
        {
            var tested = new List<string>();
            foreach (var kvp in ProviderConfigs)
            {
                if (kvp.Value.IsTested && !string.IsNullOrWhiteSpace(kvp.Key))
                {
                    tested.Add(kvp.Key);
                }
            }

            if (tested.Count == 0 && !string.IsNullOrWhiteSpace(Provider))
            {
                tested.Add(Provider);
            }

            return tested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public string? GetApiKeyForProvider(string? providerStr = null)
        {
            string provider = providerStr ?? Provider;
            if (!string.IsNullOrWhiteSpace(provider) && ProviderApiKeys.TryGetValue(provider, out string? key) && !string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
            return ApiKey;
        }

        public void SetApiKeyForProvider(string providerStr, string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ProviderApiKeys.Remove(providerStr);
            }
            else
            {
                ProviderApiKeys[providerStr] = apiKey.Trim();
            }

            if (providerStr.Equals(Provider, StringComparison.OrdinalIgnoreCase))
            {
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            }
        }

        public string? DefaultModel { get; set; }
        public string? DefaultVisionModel { get; set; }
        public string TargetLanguage { get; set; } = "Spanish";
        public string OutputDirectory { get; set; } = "output";
        public string LastScanDirectory { get; set; } = ".";
        public bool Debug { get; set; } = false;
        public string? AdditionalPrompt { get; set; }
        public int TranslationTimeoutSeconds { get; set; } = 300;
        public int PromptImproveTimeoutSeconds { get; set; } = 300;
        public int ModelCheckTimeoutSeconds { get; set; } = 5;
        public double Temperature { get; set; } = 0.3;
        public int MaxCharactersPerChunk { get; set; } = 6000;
        public int ChunkOverlapCharacters { get; set; } = 300;
        public bool PreserveFormat { get; set; } = true;
        public bool EnableReview { get; set; } = false;
        public string? ReviewModel { get; set; }
        public int ReviewTimeoutSeconds { get; set; } = 3000;
        public double ReviewTemperature { get; set; } = 0.3;
        public double OcrTemperature { get; set; } = 0.2;
        public int OcrTimeoutSeconds { get; set; } = 300;
        public string OutputFormats { get; set; } = "md";
        public bool SaveMarkdown { get; set; } = true;
        public bool ModuleExtractionEnabled { get; set; } = true;
        public bool ModuleTranslationEnabled { get; set; } = true;
        public bool ModuleReviewEnabled { get; set; } = true;
        public bool ModuleConversionEnabled { get; set; } = true;
        public string? DefaultOutputFormat { get; set; }
        public List<string> SupportedOutputFormats { get; set; } = new List<string> { "html", "docx", "odf", "pdf" };
        public List<string> SupportedInputExtensions { get; set; } = new List<string>
        {
            ".pdf", ".docx", ".doc", ".odt", ".odf", ".txt", ".md",
            ".json", ".csv", ".xml", ".html", ".jpg", ".jpeg", ".png", ".bmp", ".tiff"
        };
        public string LogDirectory { get; set; } = "logs";
        public string LogLevelConsole { get; set; } = "Information";
        public string LogLevelFile { get; set; } = "Debug";

        // --- Actualizaciones automáticas (solo aplica a instalaciones Inno
        //     Setup / .exe; en MSIX lo gestiona Microsoft Store) ---

        /// <summary>
        /// Si está activo, PolyglotCLI consulta periódicamente la última
        /// release de GitHub y notifica al usuario cuando hay una versión
        /// nueva. Por defecto <c>true</c>.
        /// </summary>
        public bool UpdateCheckEnabled { get; set; } = true;

        /// <summary>
        /// Intervalo en horas entre comprobaciones automáticas. Por
        /// defecto 6h (prudente: respeta el rate limit de GitHub sin
        /// molestar al usuario). Valor mínimo 1.
        /// </summary>
        public int UpdateCheckIntervalHours { get; set; } = 6;

        /// <summary>
        /// Marca de tiempo (UTC) de la última comprobación de updates.
        /// Se persiste en <c>config.json</c> para que la próxima ejecución
        /// sepa cuánto tiempo ha pasado desde el último check sin
        /// pegarle a la API innecesariamente.
        /// </summary>
        public DateTime? LastUpdateCheckUtc { get; set; }

        /// <summary>
        /// Versión a la que el usuario eligió <i>recordar</i> la
        /// actualización (p.ej. "1.2.0") para que no le vuelva a salir
        /// el aviso en próximos arranques. <c>null</c> = no ignorar
        /// ninguna.
        /// </summary>
        public string? DismissedUpdateVersion { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? LoadedFromPath { get; set; }

        /// <summary>
        /// UTC en que la migración del árbol legacy
        /// (<c>%AppData%\PolyglotCLI\</c> →
        /// <c>%AppData%\FittyAr\PolyglotCLI\</c>) corrió por
        /// última vez. Null si nunca migró. La UI lo usa para
        /// mostrar un aviso en la pestaña About: las API keys del
        /// config viejo se descartaron y hay que re-ingresarlas.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime? LastMigrationUtc { get; private set; }

        /// <summary>
        /// True si el caller quiere usar el config del project tree
        /// (currentDir/baseDir) en lugar del canónico en %AppData%.
        /// Útil para desarrollo. Se activa con la env var
        /// <c>POLYGLOTCLI_USE_PROJECT_CONFIG=1</c>.
        /// </summary>
        public static bool UseProjectConfig =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POLYGLOTCLI_USE_PROJECT_CONFIG"));

        [System.Text.Json.Serialization.JsonIgnore]
        public string AbsoluteOutputDirectory => GetSafeOutputDirectory(OutputDirectory);

        public static bool IsPathInsideProgramFiles(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string fullPath = Path.GetFullPath(path);
                var pathsToCheck = new List<string>();

                if (OperatingSystem.IsWindows())
                {
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (!string.IsNullOrEmpty(pf)) pathsToCheck.Add(pf);

                    string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    if (!string.IsNullOrEmpty(pfx86)) pathsToCheck.Add(pfx86);
                }

                // Check if the application base directory is in Program Files
                string baseDir = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir) && baseDir.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
                {
                    pathsToCheck.Add(baseDir);
                }

                foreach (var p in pathsToCheck)
                {
                    if (fullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return false;
        }

        public static string GetSafeOutputDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return GetDefaultOutputDirectory();
            }

            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

            if (IsPathInsideProgramFiles(fullPath))
            {
                return GetDefaultOutputDirectory();
            }

            return fullPath;
        }

        /// <summary>
        /// Carpeta por defecto para salidas (markdown, docx, exports).
        /// Siempre bajo {AppData}\FittyAr\PolyglotCLI\output\ para
        /// mantener la convención {desarrollador}\{programa}.
        /// </summary>
        public static string GetDefaultOutputDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "FittyAr", "PolyglotCLI", "output");
            }
            return Path.Combine(AppContext.BaseDirectory, "output");
        }

        public static string GetDefaultConfigPath()
        {
            if (OperatingSystem.IsWindows())
            {
                // Estructura {desarrollador}\{programa}: el árbol
                // del usuario queda aislado bajo "FittyAr\PolyglotCLI"
                // en lugar de plantar la app directo en AppData\Roaming.
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "FittyAr", "PolyglotCLI");
                // Migración one-shot desde el árbol viejo
                // (AppData\Roaming\PolyglotCLI) — preserva logs/jobs
                // existentes y resetea config.json a un estado blanco.
                // Solo aplica en AppData: si el caller está usando
                // project config (POLYGLOTCLI_USE_PROJECT_CONFIG=1),
                // no tocamos el árbol del usuario.
                if (!UseProjectConfig)
                {
                    MigrateLegacyAppDataIfNeeded();
                }
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "config.json");
            }
            return Path.Combine(AppContext.BaseDirectory, "config.json");
        }

        /// <summary>
        /// Migra los datos del usuario desde el árbol legacy
        /// <c>%AppData%\PolyglotCLI\</c> al nuevo
        /// <c>%AppData%\FittyAr\PolyglotCLI\</c>. Mueve todo el
        /// contenido EXCEPTO <c>config.json</c> (que se descarta
        /// intencionalmente para que el nuevo arranque con un
        /// config "blanco" sin keys ni paths personalizados).
        /// Idempotente: usa un archivo marker <c>.migrated</c> en
        /// la nueva ubicación.
        /// </summary>
        /// <param name="appDataOverride">
        /// Solo para tests: si se pasa, se usa como raíz en lugar
        /// de <see cref="Environment.SpecialFolder.ApplicationData"/>.
        /// En producción, dejar null.
        /// </param>
        public static void MigrateLegacyAppDataIfNeeded(string? appDataOverride = null)
        {
            if (!OperatingSystem.IsWindows()) return;

            string appData = appDataOverride
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string oldDir = Path.Combine(appData, "PolyglotCLI");
            string newDir = Path.Combine(appData, "FittyAr", "PolyglotCLI");
            string marker = Path.Combine(newDir, ".migrated");

            // Idempotencia: si ya corrió, no hace nada.
            if (File.Exists(marker)) return;

            try
            {
                // Asegurar que el destino exista — incluso si no hay
                // nada viejo, escribimos el marker así no volvemos a
                // entrar acá en cada Load.
                Directory.CreateDirectory(newDir);

                if (Directory.Exists(oldDir))
                {
                    AppLogger.Info($"Migrando datos de {oldDir} → {newDir} (config.json se descarta a propósito).");

                    // Mover todo lo del viejo al nuevo, EXCEPTO
                    // config.json (que contiene info personal del
                    // usuario: keys, paths, etc. — debe quedar
                    // blanco en el destino).
                    foreach (var srcPath in Directory.EnumerateFiles(oldDir, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(oldDir, srcPath);

                        if (string.Equals(rel, "config.json", StringComparison.OrdinalIgnoreCase))
                        {
                            // Borrar el config viejo sin dejar backup
                            // (por seguridad: contenía API keys).
                            try { File.Delete(srcPath); }
                            catch (Exception ex)
                            {
                                AppLogger.Warn($"Migración: no se pudo borrar config.json viejo: {ex.Message}");
                            }
                            continue;
                        }

                        string destPath = Path.Combine(newDir, rel);
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                        try
                        {
                            // Si el destino ya existe (caso raro de
                            // migración parcial previa), no pisamos.
                            if (File.Exists(destPath)) continue;
                            File.Move(srcPath, destPath);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Warn($"Migración: no se pudo mover {rel}: {ex.Message}");
                        }
                    }

                    // Limpiar subdirectorios vacíos del viejo.
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(oldDir, "*", SearchOption.AllDirectories)
                                                     .OrderByDescending(d => d.Length))
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                try { Directory.Delete(dir); } catch { /* best effort */ }
                            }
                        }
                        if (Directory.Exists(oldDir) && !Directory.EnumerateFileSystemEntries(oldDir).Any())
                        {
                            try { Directory.Delete(oldDir); } catch { /* best effort */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Migración: limpieza del árbol viejo falló: {ex.Message}");
                    }
                }

                // Marker: garantiza que no volvemos a migrar.
                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Migración de AppData falló: {ex.Message}", ex);
            }
        }

        public static AppConfig Load(string? configPath = null)
        {
            string resolvedPath;

            if (configPath != null)
            {
                resolvedPath = configPath;
            }
            else
            {
                // Orden de prioridad:
                //   1. AppData (canónico para datos de usuario).
                //   2. currentDir / baseDir (dev / legacy).
                //
                // Developers pueden setear POLYGLOTCLI_USE_PROJECT_CONFIG=1
                // para invertir el orden y usar el config del project
                // tree, ignorando AppData. Útil cuando se quiere
                // iterar sobre el config sin tocar el del usuario.
                string currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                string baseDirConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
                string appDataConfig = GetDefaultConfigPath();

                if (!UseProjectConfig && File.Exists(appDataConfig))
                {
                    resolvedPath = appDataConfig;
                }
                else if (File.Exists(currentDirConfig) && !IsPathInsideProgramFiles(currentDirConfig))
                {
                    resolvedPath = currentDirConfig;
                }
                else if (File.Exists(baseDirConfig) && !IsPathInsideProgramFiles(baseDirConfig))
                {
                    resolvedPath = baseDirConfig;
                }
                else if (File.Exists(baseDirConfig))
                {
                    resolvedPath = baseDirConfig;
                }
                else
                {
                    resolvedPath = appDataConfig; // Fallback default path for saving new config
                }
            }

            string savePath = resolvedPath;
            if (IsPathInsideProgramFiles(resolvedPath))
            {
                savePath = GetDefaultConfigPath();
            }

            AppConfig config;
            if (!File.Exists(resolvedPath))
            {
                config = new AppConfig();
                config.LoadedFromPath = savePath;
                config.LastMigrationUtc = ReadMigrationTimestamp();
                return config;
            }

            try
            {
                string jsonString = File.ReadAllText(resolvedPath);
                config = JsonSerializer.Deserialize<AppConfig>(jsonString) ?? new AppConfig();
                // Descifra los campos sensibles (ApiKey, ProviderApiKeys[*],
                // ProviderConfigs[*].ApiKey) que quedaron cifrados en el
                // último Save. Si todavía no había ninguno cifrado
                // (migración desde config antiguo), Unprotect es no-op.
                SecureField.UnprotectInPlace(config);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to load config.json, using defaults. Error: {ex.Message}");
                Console.ResetColor();
                config = new AppConfig();
            }

            config.LoadedFromPath = savePath;
            config.LastMigrationUtc = ReadMigrationTimestamp();
            return config;
        }

        /// <summary>
        /// Lee el timestamp de la última migración del árbol legacy,
        /// si existe. Null si nunca migró.
        /// </summary>
        public static DateTime? ReadMigrationTimestamp()
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string marker = Path.Combine(appData, "FittyAr", "PolyglotCLI", ".migrated");
                if (!File.Exists(marker)) return null;
                string text = File.ReadAllText(marker);
                if (DateTime.TryParse(text, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt;
                }
            }
            catch
            {
                // No hacer nada: el marker es best-effort, no debe
                // romper el Load.
            }
            return null;
        }

        public void Save(string? configPath = null)
        {
            configPath ??= LoadedFromPath ?? GetDefaultConfigPath();

            // Ciframos los campos sensibles (ApiKey, ProviderApiKeys[*],
            // ProviderConfigs[*].ApiKey) antes de serializar. El
            // try/finally garantiza que el estado en memoria vuelva a
            // quedar en claro aunque la escritura a disco falle: la
            // app necesita los valores reales para hablar con el LLM.
            try
            {
                SecureField.ProtectInPlace(this);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(this, options);
                // Escritura atómica: escribimos a un .tmp en la misma carpeta
                // y luego File.Replace (que es atómico en Windows/NTFS). Si el
                // proceso se corta a mitad de un File.WriteAllText clásico, el
                // config.json quedaría corrupto y el próximo Load cae a
                // defaults. Con .tmp + Replace, o se ve la versión vieja o
                // se ve la nueva, nunca un archivo trunco.
                string dir = Path.GetDirectoryName(configPath) ?? ".";
                string tmpPath = Path.Combine(dir, $".{Path.GetFileName(configPath)}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(tmpPath, jsonString);
                if (File.Exists(configPath))
                {
                    File.Replace(tmpPath, configPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tmpPath, configPath);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to save config.json. Error: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                // Devolver los campos sensibles a plaintext en memoria,
                // independientemente de si la escritura a disco tuvo
                // éxito. La app los necesita en claro para hablar con
                // el LLM.
                SecureField.UnprotectInPlace(this);
            }
        }

        public void Reload()
        {
            var fresh = Load(LoadedFromPath);
            Provider = fresh.Provider;
            OcrProvider = fresh.OcrProvider ?? fresh.Provider;
            TranslationProvider = fresh.TranslationProvider ?? fresh.Provider;
            ReviewProvider = fresh.ReviewProvider ?? fresh.Provider;
            ApiUrl = fresh.ApiUrl;
            ApiKey = fresh.ApiKey;
            ProviderApiKeys = fresh.ProviderApiKeys ?? new Dictionary<string, string>();
            ProviderConfigs = fresh.ProviderConfigs ?? new Dictionary<string, ProviderConfig>();
            DefaultModel = fresh.DefaultModel;
            DefaultVisionModel = fresh.DefaultVisionModel;
            TargetLanguage = fresh.TargetLanguage;
            OutputDirectory = fresh.OutputDirectory;
            LastScanDirectory = fresh.LastScanDirectory;
            Debug = fresh.Debug;
            AdditionalPrompt = fresh.AdditionalPrompt;
            TranslationTimeoutSeconds = fresh.TranslationTimeoutSeconds;
            PromptImproveTimeoutSeconds = fresh.PromptImproveTimeoutSeconds;
            ModelCheckTimeoutSeconds = fresh.ModelCheckTimeoutSeconds;
            Temperature = fresh.Temperature;
            MaxCharactersPerChunk = fresh.MaxCharactersPerChunk;
            ChunkOverlapCharacters = fresh.ChunkOverlapCharacters;
            PreserveFormat = fresh.PreserveFormat;
            EnableReview = fresh.EnableReview;
            ReviewModel = fresh.ReviewModel;
            ReviewTimeoutSeconds = fresh.ReviewTimeoutSeconds;
            ReviewTemperature = fresh.ReviewTemperature;
            OcrTemperature = fresh.OcrTemperature;
            OcrTimeoutSeconds = fresh.OcrTimeoutSeconds;
            OutputFormats = fresh.OutputFormats;
            SaveMarkdown = fresh.SaveMarkdown;
            DefaultOutputFormat = fresh.DefaultOutputFormat;
            SupportedOutputFormats = fresh.SupportedOutputFormats;
            SupportedInputExtensions = fresh.SupportedInputExtensions;
            LogDirectory = fresh.LogDirectory;
            LogLevelConsole = fresh.LogLevelConsole;
            LogLevelFile = fresh.LogLevelFile;
        }

        public void SavePresets(
            string lastScanDirectory,
            string? additionalPrompt,
            bool enableReview,
            bool generateDoc,
            string? selectedFormat)
        {
            LastScanDirectory = lastScanDirectory;
            AdditionalPrompt = additionalPrompt;
            EnableReview = enableReview;

            string? selectedFmt = selectedFormat?.Trim().ToLowerInvariant();
            DefaultOutputFormat = generateDoc && !string.IsNullOrEmpty(selectedFmt) ? selectedFmt : null;

            var outputFormats = new List<string>();
            if (SaveMarkdown) outputFormats.Add("md");
            if (generateDoc && !string.IsNullOrEmpty(selectedFmt)) outputFormats.Add(selectedFmt);
            if (outputFormats.Count == 0) outputFormats.Add("md");
            OutputFormats = string.Join(",", outputFormats);

            Save();
        }

        public void UpdateAndSaveSettings(
            string provider,
            string apiUrl,
            string? apiKey,
            int modelCheckTimeoutSeconds,
            string outputDirectory,
            bool debug,
            string? defaultVisionModel,
            double ocrTemperature,
            int ocrTimeoutSeconds,
            string? defaultModel,
            string targetLanguage,
            double temperature,
            int maxCharactersPerChunk,
            int chunkOverlapCharacters,
            bool preserveFormat,
            int translationTimeoutSeconds,
            bool enableReview,
            string? reviewModel,
            double reviewTemperature,
            int reviewTimeoutSeconds,
            bool saveMarkdown,
            string? defaultOutputFormat)
        {
            Provider = string.IsNullOrWhiteSpace(provider) ? "LmStudio" : provider.Trim();
            ApiUrl = apiUrl;
            SetApiKeyForProvider(Provider, apiKey);
            ModelCheckTimeoutSeconds = modelCheckTimeoutSeconds;
            OutputDirectory = outputDirectory;
            Debug = debug;

            DefaultVisionModel = string.IsNullOrWhiteSpace(defaultVisionModel) ? null : defaultVisionModel.Trim();
            OcrTemperature = ocrTemperature;
            OcrTimeoutSeconds = ocrTimeoutSeconds;

            DefaultModel = string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel.Trim();
            TargetLanguage = targetLanguage;
            Temperature = temperature;
            MaxCharactersPerChunk = maxCharactersPerChunk;
            ChunkOverlapCharacters = chunkOverlapCharacters;
            PreserveFormat = preserveFormat;
            TranslationTimeoutSeconds = translationTimeoutSeconds;

            EnableReview = enableReview;
            ReviewModel = string.IsNullOrWhiteSpace(reviewModel) ? null : reviewModel.Trim();
            ReviewTemperature = reviewTemperature;
            ReviewTimeoutSeconds = reviewTimeoutSeconds;

            SaveMarkdown = saveMarkdown;
            string? selectedFmt = defaultOutputFormat?.Trim().ToLowerInvariant() ?? "none";
            DefaultOutputFormat = selectedFmt == "none" ? null : selectedFmt;

            var selectedFormats = new List<string>();
            if (SaveMarkdown) selectedFormats.Add("md");
            if (!string.IsNullOrEmpty(DefaultOutputFormat)) selectedFormats.Add(DefaultOutputFormat);
            if (selectedFormats.Count == 0) selectedFormats.Add("md");
            OutputFormats = string.Join(",", selectedFormats);

            Save();
        }
    }
}
