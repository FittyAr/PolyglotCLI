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
        public int ReviewTimeoutSeconds { get; set; } = 300;
        public string OutputFormats { get; set; } = "md";

        public static AppConfig Load(string? configPath = null)
        {
            configPath ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            
            // Fallback to project root directory config if not found in output directory
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
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
            configPath ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            
            if (!File.Exists(configPath))
            {
                var rootConfig = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                if (File.Exists(rootConfig))
                {
                    configPath = rootConfig;
                }
            }

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
