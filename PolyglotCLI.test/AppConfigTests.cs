using System;
using System.IO;
using System.Collections.Generic;
using Xunit;
using PolyglotCLI;

namespace PolyglotCLI.test
{
    public class AppConfigTests : IDisposable
    {
        private readonly string _tempConfigFile;

        public AppConfigTests()
        {
            _tempConfigFile = Path.Combine(Path.GetTempPath(), $"config_test_{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tempConfigFile))
            {
                File.Delete(_tempConfigFile);
            }
        }

        [Fact]
        public void AppConfig_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var config = new AppConfig();

            // Assert
            Assert.Equal("LmStudio", config.Provider);
            Assert.Equal("Spanish", config.TargetLanguage);
            Assert.Equal("output", config.OutputDirectory);
            Assert.True(config.PreserveFormat);
            Assert.False(config.EnableReview);
            Assert.True(config.ModuleExtractionEnabled);
        }

        [Fact]
        public void SetApiKeyForProvider_StoresKeyInProviderApiKeys()
        {
            // Arrange
            var config = new AppConfig();
            string provider = "Gemini";
            string apiKey = "test-api-key-123";

            // Act
            config.SetApiKeyForProvider(provider, apiKey);

            // Assert
            Assert.Equal(apiKey, config.GetApiKeyForProvider(provider));
        }

        [Fact]
        public void SetApiKeyForProvider_UpdatesGlobalApiKey_WhenProviderIsActive()
        {
            // Arrange
            var config = new AppConfig { Provider = "Gemini" };
            string apiKey = "active-key-456";

            // Act
            config.SetApiKeyForProvider("Gemini", apiKey);

            // Assert
            Assert.Equal(apiKey, config.ApiKey);
        }

        [Fact]
        public void SaveAndLoad_PersistsConfigurationCorrectly()
        {
            // Arrange
            var config = new AppConfig
            {
                LoadedFromPath = _tempConfigFile,
                Provider = "Ollama",
                TargetLanguage = "French",
                ApiKey = "ollama-dummy-key"
            };

            // Act - Save config to temp file
            config.Save();

            // Act - Load from the temp file
            var loadedConfig = AppConfig.Load(_tempConfigFile);

            // Assert
            Assert.NotNull(loadedConfig);
            Assert.Equal("Ollama", loadedConfig.Provider);
            Assert.Equal("French", loadedConfig.TargetLanguage);
            Assert.Equal("ollama-dummy-key", loadedConfig.ApiKey);
        }

        [Fact]
        public void SaveTestedProvider_AddsProviderToConfigsAndSaves()
        {
            // Arrange
            var config = new AppConfig
            {
                LoadedFromPath = _tempConfigFile,
                Provider = "OpenAI"
            };
            var models = new List<string> { "gpt-4o", "gpt-4-turbo" };

            // Act
            config.SaveTestedProvider("OpenAI", "https://api.openai.com/v1", "openai-key", models);

            // Assert
            Assert.Contains("OpenAI", config.GetTestedProviders());
            var openaiCfg = config.GetProviderConfig("OpenAI");
            Assert.True(openaiCfg.IsTested);
            Assert.Equal("https://api.openai.com/v1", openaiCfg.ApiUrl);
            Assert.Equal(models, openaiCfg.AvailableModels);
        }
    }
}
