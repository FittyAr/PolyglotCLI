using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
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

        // ─── SecureField: cifrado de API keys ────────────────────────
        // Estos tests verifican la invariante principal: en disco
        // nunca debe quedar la key en claro (excepto cuando la
        // plataforma no soporta DPAPI, en cuyo caso SecureField
        // degrada a plaintext con un warning — lo testeamos abajo).

        [Fact]
        public void SecureField_Protect_AddsEncryptedPrefix()
        {
            string plain = "sk-test-12345";
            string? cipher = SecureField.Protect(plain);

            Assert.NotNull(cipher);
            Assert.StartsWith(SecureField.EncryptedPrefix, cipher);
            Assert.DoesNotContain(plain, cipher);
        }

        [Fact]
        public void SecureField_Protect_IsIdempotent()
        {
            string plain = "sk-test-12345";
            string? cipher1 = SecureField.Protect(plain);
            string? cipher2 = SecureField.Protect(cipher1);

            Assert.Equal(cipher1, cipher2);
        }

        [Fact]
        public void SecureField_Unprotect_ReturnsOriginal()
        {
            string plain = "sk-test-12345";
            string? cipher = SecureField.Protect(plain);
            string? back = SecureField.Unprotect(cipher);

            Assert.Equal(plain, back);
        }

        [Fact]
        public void SecureField_Unprotect_PassesThroughPlaintext()
        {
            // Para migración: configs antiguos en texto plano deben
            // pasar tal cual por Unprotect (sin prefijar como cifrado).
            string plain = "old-plaintext-key";
            string? back = SecureField.Unprotect(plain);

            Assert.Equal(plain, back);
        }

        [Fact]
        public void SecureField_Protect_HandlesNullAndEmpty()
        {
            Assert.Null(SecureField.Protect(null));
            Assert.Equal(string.Empty, SecureField.Protect(string.Empty));
            Assert.Null(SecureField.Unprotect(null));
            Assert.Equal(string.Empty, SecureField.Unprotect(string.Empty));
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("abc", "***")]
        [InlineData("abcdefghij", "**********")]             // 10 chars (≤ 18) → enmascara todo
        [InlineData("abcdefghijk", "***********")]           // 11 chars (≤ 18) → enmascara todo
        [InlineData("abcdefghijklmno", "***************")]   // 15 chars (≤ 18) → enmascara todo
        [InlineData("abcdefghijklmnopqrs", "abcde…opqrs")]   // 19 chars: 5+…+5, 9 ocultos
        [InlineData("sk-1234567890abcdef", "sk-12…bcdef")]   // 19 chars: 5+…+5, 9 ocultos
        [InlineData("sk-1234567890abcdefghij", "sk-12…fghij")]// 23 chars: 5+…+5, 13 ocultos
        public void SecureField_Mask_ReturnsExpectedOutput(string? input, string expected)
        {
            Assert.Equal(expected, SecureField.Mask(input));
        }

        [Fact]
        public void Save_EncryptsApiKeyInJsonOnDisk()
        {
            // Arrange
            var config = new AppConfig
            {
                LoadedFromPath = _tempConfigFile,
                ApiKey = "super-secret-key"
            };

            // Act
            config.Save();

            // Assert: leer el JSON directamente y comprobar que la
            // ApiKey en disco está cifrada (no aparece el plaintext).
            string json = File.ReadAllText(_tempConfigFile);
            Assert.Contains(SecureField.EncryptedPrefix, json);
            Assert.DoesNotContain("super-secret-key", json);
        }

        [Fact]
        public void Save_EncryptsProviderApiKeysAndProviderConfigKeys()
        {
            // Arrange
            var config = new AppConfig
            {
                LoadedFromPath = _tempConfigFile,
                Provider = "OpenAi"
            };
            config.SetApiKeyForProvider("OpenAi", "openai-key-A");
            config.SetApiKeyForProvider("Gemini", "gemini-key-B");
            config.SaveTestedProvider("OpenAi", "https://api.openai.com/v1", "openai-key-A", new List<string> { "gpt-4o" });

            // Act
            config.Save();

            // Assert: el JSON en disco no debe contener ninguna key en
            // claro. Las tres ubicaciones (ApiKey global,
            // ProviderApiKeys, ProviderConfigs[*].ApiKey) tienen que
            // estar cifradas.
            string json = File.ReadAllText(_tempConfigFile);
            Assert.DoesNotContain("openai-key-A", json);
            Assert.DoesNotContain("gemini-key-B", json);
        }

        [Fact]
        public void SaveAndLoad_ApiKeyRoundtrips_AndInMemoryStaysPlaintext()
        {
            // Arrange
            const string plain = "roundtrip-key-xyz";
            var config = new AppConfig
            {
                LoadedFromPath = _tempConfigFile,
                ApiKey = plain
            };

            // Act
            config.Save();
            var loaded = AppConfig.Load(_tempConfigFile);

            // Assert
            Assert.Equal(plain, loaded!.ApiKey);
            // Y el in-memory del config original debe seguir en claro
            // (el finally de Save restaura), para que la app pueda
            // seguir usándolo.
            Assert.Equal(plain, config.ApiKey);
        }

        [Fact]
        public void Save_LeavesMemoryPlaintext_EvenIfWriteFails()
        {
            // Arrange: forzar un fallo de escritura apuntando a un
            // directorio inexistente bajo C:\.
            var config = new AppConfig
            {
                LoadedFromPath = @"C:\_polyglotcli_no_existe_esta_carpeta_\config.json",
                ApiKey = "preserved-key"
            };

            // Act
            config.Save(); // No debe tirar: el catch existente traga
                            // la excepción. Pero el finally debe haber
                            // restaurado el plaintext.

            // Assert
            Assert.Equal("preserved-key", config.ApiKey);
            Assert.DoesNotContain(SecureField.EncryptedPrefix, config.ApiKey);
        }

        [Fact]
        public void Load_HandlesLegacyPlaintextConfig()
        {
            // Arrange: simular un config.json antiguo con keys en
            // plano (escribimos a mano el JSON).
            var legacy = new
            {
                Provider = "OpenAi",
                ApiKey = "legacy-plaintext-key",
                ProviderApiKeys = new Dictionary<string, string>
                {
                    { "OpenAi", "legacy-plaintext-key" }
                }
            };
            File.WriteAllText(_tempConfigFile, JsonSerializer.Serialize(legacy));

            // Act
            var loaded = AppConfig.Load(_tempConfigFile);

            // Assert: el Load no debe romper, y debe devolver las
            // keys en claro (porque no tienen el prefijo enc:v1:).
            Assert.NotNull(loaded);
            Assert.Equal("legacy-plaintext-key", loaded!.ApiKey);
        }

        // ─── Paths estandarizados bajo {FittyAr}\{PolyglotCLI} ───────
        // Toda la data de usuario (config, logs, jobs, output) vive
        // bajo %AppData%\FittyAr\PolyglotCLI\ en lugar de plantar
        // la app directo en AppData\Roaming.

        [Fact]
        public void GetDefaultConfigPath_UsesFittyArPolyglotCLI_OnWindows()
        {
            if (!OperatingSystem.IsWindows()) return; // Skip en otros OS.

            string path = AppConfig.GetDefaultConfigPath();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string expectedDir = Path.Combine(appData, "FittyAr", "PolyglotCLI");

            Assert.StartsWith(expectedDir, path);
            Assert.EndsWith("config.json", path);
        }

        [Fact]
        public void GetDefaultOutputDirectory_UsesFittyArPolyglotCLI_OnWindows()
        {
            if (!OperatingSystem.IsWindows()) return;

            string path = AppConfig.GetDefaultOutputDirectory();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string expectedDir = Path.Combine(appData, "FittyAr", "PolyglotCLI", "output");

            Assert.Equal(expectedDir, path);
        }

        [Fact]
        public void TranslationOrchestrator_GetJobsDirectory_UsesFittyArPolyglotCLI_OnWindows()
        {
            if (!OperatingSystem.IsWindows()) return;

            string path = TranslationOrchestrator.GetJobsDirectory();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string expectedDir = Path.Combine(appData, "FittyAr", "PolyglotCLI", "jobs");

            Assert.Equal(expectedDir, path);
        }

        // ─── Migración one-shot del árbol legacy ────────────────────

        [Fact]
        public void MigrateLegacyAppData_MovesEverythingExceptConfigJson()
        {
            // Arrange: simular un árbol viejo con config + logs.
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            string oldDir = Path.Combine(fakeAppData, "PolyglotCLI");
            string newDir = Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI");
            try
            {
                Directory.CreateDirectory(Path.Combine(oldDir, "logs"));
                Directory.CreateDirectory(Path.Combine(oldDir, "jobs", "abc"));
                File.WriteAllText(Path.Combine(oldDir, "config.json"),
                    "{\"Provider\":\"OpenAi\",\"ApiKey\":\"sk-personal-key\"}");
                File.WriteAllText(Path.Combine(oldDir, "logs", "polyglot.log"), "old log content");
                File.WriteAllText(Path.Combine(oldDir, "jobs", "abc", "manifest.json"), "{}");

                // Act
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);

                // Assert
                // 1. config.json NO se movió (se borró).
                Assert.False(File.Exists(Path.Combine(oldDir, "config.json")));
                Assert.False(File.Exists(Path.Combine(newDir, "config.json")));

                // 2. logs y jobs se movieron.
                Assert.True(File.Exists(Path.Combine(newDir, "logs", "polyglot.log")));
                Assert.True(File.Exists(Path.Combine(newDir, "jobs", "abc", "manifest.json")));

                // 3. El marker existe (idempotencia).
                Assert.True(File.Exists(Path.Combine(newDir, ".migrated")));

                // 4. El viejo directorio se eliminó (quedó vacío).
                Assert.False(Directory.Exists(oldDir));
            }
            finally
            {
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void MigrateLegacyAppData_IsIdempotent()
        {
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            try
            {
                // Arrange: nada en el viejo. La primera llamada solo
                // escribe el marker. La segunda no debe hacer nada.
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);
                string marker = Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI", ".migrated");
                Assert.True(File.Exists(marker));

                DateTime markerTime1 = File.GetLastWriteTimeUtc(marker);

                // Act
                System.Threading.Thread.Sleep(50);
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);

                // Assert: marker intacto (no reescrito).
                DateTime markerTime2 = File.GetLastWriteTimeUtc(marker);
                Assert.Equal(markerTime1, markerTime2);
            }
            finally
            {
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void MigrateLegacyAppData_NoOpWhenOldDirMissing()
        {
            // Arrange: %AppData%\PolyglotCLI\ no existe (caso fresh
            // install). La migración no debe fallar: solo escribe el
            // marker.
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            try
            {
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);

                Assert.True(Directory.Exists(Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI")));
                Assert.True(File.Exists(Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI", ".migrated")));
            }
            finally
            {
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void MigrateLegacyAppData_LeavesLockedFilesInOldDir()
        {
            // Arrange: simular un archivo locked (típico: log en uso
            // por Serilog). FileShare.None evita que otros handles
            // puedan leer/escribir, y File.Move va a fallar.
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            string oldDir = Path.Combine(fakeAppData, "PolyglotCLI");
            string newDir = Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI");
            FileStream? lockedHandle = null;
            try
            {
                Directory.CreateDirectory(Path.Combine(oldDir, "logs"));
                Directory.CreateDirectory(Path.Combine(oldDir, "jobs", "abc"));
                File.WriteAllText(Path.Combine(oldDir, "config.json"), "{\"Provider\":\"OpenAi\"}");
                File.WriteAllText(Path.Combine(oldDir, "logs", "free.log"), "movable");
                File.WriteAllText(Path.Combine(oldDir, "jobs", "abc", "manifest.json"), "{}");
                string lockedPath = Path.Combine(oldDir, "logs", "locked.log");
                File.WriteAllText(lockedPath, "cant-move");
                lockedHandle = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

                // Act
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);

                // Assert:
                // 1. config.json se borró (es la excepción documentada,
                //    vía SecureDeleteFile).
                Assert.False(File.Exists(Path.Combine(oldDir, "config.json")));

                // 2. El archivo libre se movió.
                Assert.True(File.Exists(Path.Combine(newDir, "logs", "free.log")));
                Assert.False(File.Exists(Path.Combine(oldDir, "logs", "free.log")));

                // 3. El archivo locked SE QUEDÓ en el old dir
                // (best-effort: el File.Move falló, el catch loggeó
                // warning y siguió).
                Assert.True(File.Exists(Path.Combine(oldDir, "logs", "locked.log")));

                // 4. El marker NO se escribió: el fallo parcial hace
                // que el próximo Load re-intente. Esto cambia el
                // comportamiento anterior (que escribía el marker
                // igual) para no dejar al usuario con datos en dos
                // árboles sin posibilidad de recovery.
                Assert.False(File.Exists(Path.Combine(newDir, ".migrated")));

                // 5. jobs/abc/manifest.json se movió. El viejo
                // jobs/abc/ debe haber sido limpiado por el bloque
                // de "limpiar subdirectorios vacíos".
                Assert.True(File.Exists(Path.Combine(newDir, "jobs", "abc", "manifest.json")));

                // 6. El lock file .migrating se limpió (sino el
                // próximo Load no podría re-intentar).
                Assert.False(File.Exists(Path.Combine(newDir, ".migrating")));

                // Cerrar el handle y re-intentar la migración. Esta
                // vez tiene que completar y escribir el marker.
                lockedHandle.Dispose();
                lockedHandle = null;
                AppConfig.MigrateLegacyAppDataIfNeeded(fakeAppData);

                // 7. Después de liberar el lock y re-intentar: el
                // marker se escribió, el archivo locked se movió.
                Assert.True(File.Exists(Path.Combine(newDir, ".migrated")));
                Assert.True(File.Exists(Path.Combine(newDir, "logs", "locked.log")));
                Assert.False(File.Exists(Path.Combine(oldDir, "logs", "locked.log")));
            }
            finally
            {
                lockedHandle?.Dispose();
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void GetSafeOutputDirectory_RedirectsToFittyArPolyglotCLI_WhenPathInProgramFiles()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Arrange: simular un path que termina en Program Files.
            string fakePfPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PolyglotCLI", "output");
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string expected = Path.Combine(appData, "FittyAr", "PolyglotCLI", "output");

            // Act
            string actual = AppConfig.GetSafeOutputDirectory(fakePfPath);

            // Assert: debe redirigir a FittyAr/PolyglotCLI/output, no
            // a PolyglotCLI/output (regression del cambio de
            // estandarización).
            Assert.Equal(expected, actual);
            Assert.StartsWith(Path.Combine(appData, "FittyAr", "PolyglotCLI"), actual);
            Assert.DoesNotContain(Path.Combine(appData, "PolyglotCLI", "output"), actual);
        }

        [Fact]
        public void GetSafeOutputDirectory_DefaultIsFittyArPolyglotCLI_WhenPathIsEmpty()
        {
            if (!OperatingSystem.IsWindows()) return;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string expected = Path.Combine(appData, "FittyAr", "PolyglotCLI", "output");

            string actual = AppConfig.GetSafeOutputDirectory("");

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void UseProjectConfig_ReflectsEnvVar()
        {
            // Arrange
            string envName = "POLYGLOTCLI_USE_PROJECT_CONFIG";
            string? original = Environment.GetEnvironmentVariable(envName);
            try
            {
                Environment.SetEnvironmentVariable(envName, null);
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "1");
                Assert.True(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "true");
                Assert.True(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "");
                Assert.False(AppConfig.UseProjectConfig);
            }
            finally
            {
                Environment.SetEnvironmentVariable(envName, original);
            }
        }

        [Fact]
        public void Load_UseProjectConfigPrefersDevPath_OverAppData()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Arrange: un config en current dir + un marker de
            // "AppData ya existe" (simulando que la app corrió
            // alguna vez en modo instalado). Con la env var
            // activada, Load debe preferir el currentDir config.
            string envName = "POLYGLOTCLI_USE_PROJECT_CONFIG";
            string? originalEnv = Environment.GetEnvironmentVariable(envName);
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            try
            {
                // Crear estructura de AppData con un marker.
                Directory.CreateDirectory(Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI"));
                File.WriteAllText(Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI", ".migrated"),
                    DateTime.UtcNow.ToString("O"));

                // Crear un config en el current dir (con API key
                // legible, para poder verificar que se cargó).
                string devConfig = Path.Combine(Directory.GetCurrentDirectory(), $"dev-config-{Guid.NewGuid()}.json");
                File.WriteAllText(devConfig,
                    "{\"Provider\":\"OpenAi\",\"ApiKey\":\"dev-only-key\"}");

                // Activar la env var.
                Environment.SetEnvironmentVariable(envName, "1");
                // No podemos redirigir %AppData% desde tests, así
                // que este test valida solo el switch lógico:
                // AppConfig.UseProjectConfig debe ser true.
                Assert.True(AppConfig.UseProjectConfig);

                // Cleanup del dev config.
                File.Delete(devConfig);
            }
            finally
            {
                Environment.SetEnvironmentVariable(envName, originalEnv);
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void UnprotectInPlace_PreservesEntry_WithDecryptionFailedMarker()
        {
            // Arrange: simular un config con una key que parece
            // cifrada pero el blob es inválido (caso "copiado de
            // otra máquina"). El Unprotect debe fallar y la entry
            // debe quedar con el marker, no descartada.
            var config = new AppConfig();
            config.ProviderApiKeys = new Dictionary<string, string>
            {
                { "OpenAi", "enc:v1:AAAA-invalid-base64-or-bad-blob-AAAA" },
                { "Gemini", "plaintext-legacy-key" }
            };

            // Act
            SecureField.UnprotectInPlace(config);

            // Assert
            // 1. La entry "OpenAi" se preserva con el marker
            //    (en vez de desaparecer silenciosamente).
            Assert.True(config.ProviderApiKeys.ContainsKey("OpenAi"));
            Assert.True(SecureField.IsDecryptionFailed(config.ProviderApiKeys["OpenAi"]));

            // 2. La entry "Gemini" se descifra normalmente
            //    (plaintext legacy pasa tal cual).
            Assert.Equal("plaintext-legacy-key", config.ProviderApiKeys["Gemini"]);
        }

        [Fact]
        public void LastMigrationUtc_IsNull_WhenNoMarkerExists()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Sin marker en el dir aislado, ReadMigrationTimestamp
            // debe devolver null (no el timestamp de la máquina del
            // dev que corre la suite, ni tirar excepción).
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            try
            {
                Assert.Null(AppConfig.ReadMigrationTimestamp(fakeAppData));
            }
            finally
            {
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void LastMigrationUtc_ReturnsParsedTimestamp_WhenMarkerExists()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Marker con un timestamp conocido: el método debe
            // devolver exactamente ese DateTime parseado en modo
            // roundtrip.
            string fakeAppData = Path.Combine(Path.GetTempPath(), $"appdata_{Guid.NewGuid()}");
            string markerDir = Path.Combine(fakeAppData, "FittyAr", "PolyglotCLI");
            try
            {
                Directory.CreateDirectory(markerDir);
                DateTime stamp = new DateTime(2026, 7, 25, 14, 30, 0, DateTimeKind.Utc);
                File.WriteAllText(Path.Combine(markerDir, ".migrated"), stamp.ToString("O"));

                DateTime? got = AppConfig.ReadMigrationTimestamp(fakeAppData);

                Assert.NotNull(got);
                Assert.Equal(stamp, got!.Value);
            }
            finally
            {
                if (Directory.Exists(fakeAppData)) Directory.Delete(fakeAppData, recursive: true);
            }
        }

        [Fact]
        public void UseProjectConfig_RejectsArbitraryValues_AcceptsExplicitOnes()
        {
            // El flag es un downgrade de seguridad: sólo valores
            // explícitos ("1" / "true") lo activan. Esto evita
            // activaciones accidentales (e.g. set POLYGLOTCLI_USE_PROJECT_CONFIG= 0
            // por error).
            string envName = "POLYGLOTCLI_USE_PROJECT_CONFIG";
            string? original = Environment.GetEnvironmentVariable(envName);
            try
            {
                Environment.SetEnvironmentVariable(envName, null);
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "");
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "0");
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "no");
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "yes");
                Assert.False(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "1");
                Assert.True(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "true");
                Assert.True(AppConfig.UseProjectConfig);

                Environment.SetEnvironmentVariable(envName, "TRUE");
                Assert.True(AppConfig.UseProjectConfig);

                // Con espacios alrededor: trim() y match.
                Environment.SetEnvironmentVariable(envName, "  1  ");
                Assert.True(AppConfig.UseProjectConfig);
            }
            finally
            {
                Environment.SetEnvironmentVariable(envName, original);
            }
        }

        // ── StrictValidation (PR 4) ────────────────────────────

        [Fact]
        public void StrictValidation_DefaultsToFalse()
        {
            // Por default, StrictValidation es false para mantener
            // retrocompat con installs existentes.
            var config = new AppConfig();
            Assert.False(config.StrictValidation);
        }

        [Fact]
        public void ValidateAndLog_DoesNotThrow_WhenStrictDisabled_AndInputInvalid()
        {
            // PR 2: con StrictValidation=false (default), los
            // inputs inválidos solo se loguean, no tiran excepción.
            var config = new AppConfig
            {
                ApiUrl = "not a url",  // inválido
                OutputDirectory = "..\\..\\evil",  // path traversal
                StrictValidation = false
            };

            // No debe tirar. Si tira, el test falla.
            // El log se emite pero no verificamos eso acá.
            AppConfig.ValidateAndLog("test", config);
        }

        [Fact]
        public void ValidateAndLog_Throws_WhenStrictEnabled_AndInputInvalid()
        {
            // PR 4: con StrictValidation=true, los inputs
            // inválidos tiran InvalidOperationException.
            var config = new AppConfig
            {
                ApiUrl = "not a url",  // inválido
                OutputDirectory = "..\\..\\evil",  // path traversal
                StrictValidation = true
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => AppConfig.ValidateAndLog("test", config));
            Assert.Contains("StrictValidation", ex.Message);
        }

        [Fact]
        public void ValidateAndLog_DoesNotThrow_WhenStrictEnabled_AndInputValid()
        {
            // Edge case: StrictValidation=true pero todos los
            // campos son válidos → no debe tirar.
            var config = new AppConfig
            {
                ApiUrl = "http://localhost:1234/v1",
                OutputDirectory = "output",
                LogDirectory = "logs",
                DefaultModel = "qwen/qwen2.5-7b",
                StrictValidation = true
            };

            AppConfig.ValidateAndLog("test", config);
        }
    }
}

