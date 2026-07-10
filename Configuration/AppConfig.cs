using System;
using System.IO;
using System.Text.Json;

namespace PolyglotCLI
{
    public class AppConfig
    {
        public string ApiUrl { get; set; } = "http://172.22.144.1:1234/v1";
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
    }
}
