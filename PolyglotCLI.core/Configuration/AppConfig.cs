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

        [System.Text.Json.Serialization.JsonIgnore]
        public string? LoadedFromPath { get; set; }

        public static string GetDefaultConfigPath()
        {
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "PolyglotCLI");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "config.json");
            }
            return Path.Combine(AppContext.BaseDirectory, "config.json");
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
                // Prefer local project/workspace configuration in GetCurrentDirectory() or AppContext.BaseDirectory
                string currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                string baseDirConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
                string appDataConfig = GetDefaultConfigPath();

                if (File.Exists(currentDirConfig))
                {
                    resolvedPath = currentDirConfig;
                }
                else if (File.Exists(baseDirConfig))
                {
                    resolvedPath = baseDirConfig;
                }
                else if (File.Exists(appDataConfig))
                {
                    resolvedPath = appDataConfig;
                }
                else
                {
                    resolvedPath = appDataConfig; // Fallback default path for saving new config
                }
            }

            AppConfig config;
            if (!File.Exists(resolvedPath))
            {
                config = new AppConfig();
                config.LoadedFromPath = resolvedPath;
                return config;
            }

            try
            {
                string jsonString = File.ReadAllText(resolvedPath);
                config = JsonSerializer.Deserialize<AppConfig>(jsonString) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to load config.json, using defaults. Error: {ex.Message}");
                Console.ResetColor();
                config = new AppConfig();
            }

            config.LoadedFromPath = resolvedPath;
            return config;
        }

        public void Save(string? configPath = null)
        {
            configPath ??= LoadedFromPath ?? GetDefaultConfigPath();
            
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, jsonString);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to save config.json. Error: {ex.Message}");
                Console.ResetColor();
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
