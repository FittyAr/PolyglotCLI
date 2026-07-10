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
            configPath ??= GetDefaultConfigPath();
            
            // Fallback to local files if appdata config does not exist
            if (!File.Exists(configPath))
            {
                string localPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                if (File.Exists(localPath))
                {
                    configPath = localPath;
                }
                else
                {
                    string rootPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                    if (File.Exists(rootPath))
                    {
                        configPath = rootPath;
                    }
                }
            }

            if (!File.Exists(configPath))
            {
                return new AppConfig();
            }

            try
            {
                string jsonString = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(jsonString);
                return config ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to load config.json, using defaults. Error: {ex.Message}");
                Console.ResetColor();
                return new AppConfig();
            }
        }

        public void Save(string? configPath = null)
        {
            configPath ??= GetDefaultConfigPath();
            
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
