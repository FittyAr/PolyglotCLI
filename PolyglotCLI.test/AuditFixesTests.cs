using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using PolyglotCLI;
using PolyglotCLI.Update;

// CA1416: SecureField está marcado [SupportedOSPlatform("windows")]; sus
// métodos degradan de forma segura en otros SO (ver AppConfigTests).
#pragma warning disable CA1416

namespace PolyglotCLI.test
{
    /// <summary>
    /// Regresiones de los hallazgos de la auditoría de calidad (chunking,
    /// rangos de páginas, fusión de progreso al reanudar, saneo de nombres,
    /// config sin secretos, versiones del updater, etc.).
    /// </summary>
    public class AuditFixesTests : IDisposable
    {
        private readonly string _tempRoot;

        public AuditFixesTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"polyglot-audit-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        // ---- TextChunker ----

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public async Task ChunkText_DoesNotLoopForever_WhenMaxCharactersIsNotPositive(int maxCharacters)
        {
            string longLine = new string('a', 20_000);
            var ct = TestContext.Current.CancellationToken;

            // WaitAsync lanza TimeoutException si el chunker se cuelga.
            var result = await Task.Run(() => TextChunker.ChunkText(longLine, maxCharacters, 0), ct)
                .WaitAsync(TimeSpan.FromSeconds(10), ct);

            Assert.NotEmpty(result);
            Assert.Equal(longLine, string.Concat(result));
        }

        // ---- CommandLineOptions.ParsePageRange ----

        [Theory]
        [InlineData("1-2147483647")]
        [InlineData("2147483647-1")]
        [InlineData("-5-3")]
        public async Task ParsePageRange_ClampsHugeRangesToTotalPages(string range)
        {
            var ct = TestContext.Current.CancellationToken;

            var pages = await Task.Run(() => CommandLineOptions.ParsePageRange(range, 5), ct)
                .WaitAsync(TimeSpan.FromSeconds(10), ct);

            Assert.NotNull(pages);
            Assert.All(pages!, p => Assert.InRange(p, 1, 5));
        }

        [Fact]
        public void ParsePageRange_HugeRange_ReturnsAllPages()
        {
            var pages = CommandLineOptions.ParsePageRange("1-2147483647", 3);

            Assert.Equal(new[] { 1, 2, 3 }, pages!.OrderBy(p => p).ToArray());
        }

        // ---- PageStateMerger (reanudar docs de texto) ----

        [Fact]
        public void MergeCachedProgress_KeepsTranslation_WhenChunkTextUnchanged()
        {
            var fresh = new List<PageProcessState>
            {
                new() { PageNumber = 1, OcrText = "Hello" },
                new() { PageNumber = 2, OcrText = "World" }
            };
            var cached = new List<PageProcessState>
            {
                new() { PageNumber = 1, OcrText = "Hello", TranslatedText = "Hola", ReviewedText = "Hola!", ThoughtText = "t", ReviewFailed = false },
                new() { PageNumber = 2, OcrText = "Changed", TranslatedText = "Cambiado" }
            };

            int reused = PageStateMerger.MergeCachedProgress(fresh, cached);

            Assert.Equal(1, reused);
            Assert.Equal("Hola", fresh[0].TranslatedText);
            Assert.Equal("Hola!", fresh[0].ReviewedText);
            Assert.Equal("t", fresh[0].ThoughtText);
            // El chunk 2 cambió: su traducción previa no corresponde.
            Assert.Null(fresh[1].TranslatedText);
        }

        [Fact]
        public async Task PlainTextResume_DoesNotDestroySavedTranslations()
        {
            // Simula el flujo del orquestador: extracción nueva + fusión +
            // guardado. Antes data.json quedaba sin las traducciones.
            string txt = Path.Combine(_tempRoot, "doc.txt");
            File.WriteAllText(txt, "Primera línea\nSegunda línea");
            string dataJson = Path.Combine(_tempRoot, "data", "doc_data.json");

            var extractor = new PlainTextDocumentExtractor();
            var target = new DocumentTarget { FilePath = txt, MaxCharactersPerChunk = 6000, ChunkOverlapCharacters = 0 };
            var ocr = new OcrService(new NullLlmClient(), "prompt");

            var first = await extractor.ExtractTextAsync(txt, target, ocr, new PdfPageRenderer());
            first[0].TranslatedText = "First line\nSecond line";
            first[0].ThoughtText = "razonamiento";
            JobManifestService.SavePageStatesToJson(first, dataJson);

            var cached = PageStateMapper.ToPageStates(JobManifestService.GetJobDataPages(dataJson));
            var second = await extractor.ExtractTextAsync(txt, target, ocr, new PdfPageRenderer(), null, cached);
            PageStateMerger.MergeCachedProgress(second, cached);
            JobManifestService.SavePageStatesToJson(second, dataJson);

            var reloaded = JobManifestService.GetJobDataPages(dataJson);
            Assert.Equal("First line\nSecond line", reloaded[0].TranslatedText);
            Assert.Equal("razonamiento", reloaded[0].ThoughtText);
        }

        [Fact]
        public void SavePageStatesToJson_PersistsThoughtAndReviewFlags()
        {
            string dataJson = Path.Combine(_tempRoot, "data", "x_data.json");
            var states = new List<PageProcessState>
            {
                new() { PageNumber = 1, OcrText = "a", ThoughtText = "why", ReviewFailed = true, ReviewErrorMessage = "boom" }
            };

            JobManifestService.SavePageStatesToJson(states, dataJson);
            var reloaded = PageStateMapper.ToPageStates(JobManifestService.GetJobDataPages(dataJson));

            Assert.Equal("why", reloaded[0].ThoughtText);
            Assert.True(reloaded[0].ReviewFailed);
            Assert.Equal("boom", reloaded[0].ReviewErrorMessage);
        }

        // ---- Normalización de nombres en sources/ ----

        [Fact]
        public void BuildUniqueNormalizedFileName_KeepsUnicodeLetters_AndDedupes()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string a = JobManifestService.BuildUniqueNormalizedFileName("Año español.pdf", used);
            string b = JobManifestService.BuildUniqueNormalizedFileName("Año español.pdf", used);
            string c = JobManifestService.BuildUniqueNormalizedFileName("日本語.pdf", used);

            Assert.Equal("Añoespañol.pdf", a);
            Assert.Equal("Añoespañol_2.pdf", b);
            Assert.Equal("日本語.pdf", c);
        }

        [Fact]
        public void BuildUniqueNormalizedFileName_UsesGuid_WhenNameBecomesEmpty()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string name = JobManifestService.BuildUniqueNormalizedFileName("???.pdf", used);

            Assert.EndsWith(".pdf", name);
            Assert.Equal(32 + 4, name.Length);
        }

        // ---- Config sin secretos ----

        [Fact]
        public void InitializeJobDirectory_DoesNotPersistApiKeys()
        {
            var config = new AppConfig { ApiKey = "sk-live-global-secret" };
            config.ProviderApiKeys["OpenAi"] = "sk-provider-secret";
            config.ProviderConfigs["Gemini"] = new ProviderConfig { ApiUrl = "https://example.test/v1", ApiKey = "gemini-secret" };
            string jobDir = Path.Combine(_tempRoot, "job1");

            JobManifestService.InitializeJobDirectory(jobDir, config);

            string json = File.ReadAllText(Path.Combine(jobDir, "config.json"));
            Assert.DoesNotContain("sk-live-global-secret", json);
            Assert.DoesNotContain("sk-provider-secret", json);
            Assert.DoesNotContain("gemini-secret", json);
            Assert.DoesNotContain("enc:v1:", json);
            // La instancia viva conserva sus keys.
            Assert.Equal("sk-live-global-secret", config.ApiKey);
        }

        [Fact]
        public void ExportJobPackage_StripsKeysFromLegacyConfig_AndExcludesLogs()
        {
            string jobDir = Path.Combine(_tempRoot, "legacy_job");
            Directory.CreateDirectory(Path.Combine(jobDir, "logs"));
            File.WriteAllText(Path.Combine(jobDir, "logs", "polyglot.log"), "C:\\Users\\someone");
            File.WriteAllText(Path.Combine(jobDir, "manifest.json"),
                "{\"JobId\":\"legacy_job\",\"Status\":\"Completed\",\"Files\":[]}");
            File.WriteAllText(Path.Combine(jobDir, "config.json"),
                "{\"ApiKey\":\"enc:v1:AAAASECRET\",\"ProviderApiKeys\":{\"OpenAi\":\"sk-secret\"}," +
                "\"ProviderConfigs\":{\"Gemini\":{\"ApiUrl\":\"https://x\",\"ApiKey\":\"gem-secret\"}},\"Temperature\":0.4}");

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Read);
            Assert.DoesNotContain(archive.Entries, e => e.FullName.Contains("/logs/", StringComparison.OrdinalIgnoreCase));

            var cfgEntry = archive.GetEntry("legacy_job/config.json");
            Assert.NotNull(cfgEntry);
            using var reader = new StreamReader(cfgEntry!.Open());
            string json = reader.ReadToEnd();
            Assert.DoesNotContain("SECRET", json);
            Assert.DoesNotContain("sk-secret", json);
            Assert.DoesNotContain("gem-secret", json);
            Assert.Contains("Temperature", json);
        }

        // ---- Importación de paquetes .zpg ----

        private static MemoryStream BuildPackage(string jobId, string filesJson, string outputDirectory = @"D:\\attacker")
        {
            string manifest = "{\"JobId\":\"" + jobId + "\",\"Status\":\"Completed\",\"OutputDirectory\":\"" + outputDirectory + "\",\"Files\":" + filesJson + "}";
            var ms = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(jobId + "/manifest.json");
                using var w = new StreamWriter(entry.Open());
                w.Write(manifest);
            }
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public async Task Import_KeepsSourcePathInsideSources_WhenNormalizedNameHasTraversal()
        {
            string files = @"[{""SourceFilePath"":""x"",""OriginalFileName"":""a.pdf"",""NormalizedFileName"":""..\\..\\..\\Windows\\win.ini""}]";
            string root = Path.Combine(_tempRoot, "jobs");
            Directory.CreateDirectory(root);

            using var pkg = BuildPackage("trav_job", files);
            string id = await JobPackageService.ImportJobPackageAsync(pkg, root);

            var manifest = JobManifest.Load(Path.Combine(root, id, "manifest.json"));
            string sources = Path.GetFullPath(Path.Combine(root, id, "sources"));
            string src = manifest!.Files[0].SourceFilePath;
            Assert.StartsWith(sources + Path.DirectorySeparatorChar, src);
            Assert.Equal("win.ini", Path.GetFileName(src));
        }

        [Fact]
        public async Task Import_BlanksSourcePath_WhenNoSafeNameCanBeDerived()
        {
            string files = @"[{""SourceFilePath"":"""",""OriginalFileName"":"".."",""NormalizedFileName"":""C:""}]";
            string root = Path.Combine(_tempRoot, "jobs2");
            Directory.CreateDirectory(root);

            using var pkg = BuildPackage("blank_job", files);
            string id = await JobPackageService.ImportJobPackageAsync(pkg, root);

            var manifest = JobManifest.Load(Path.Combine(root, id, "manifest.json"));
            Assert.Equal(string.Empty, manifest!.Files[0].SourceFilePath);
        }

        [Fact]
        public async Task Import_ResetsOutputDirectoryToLocalDefault()
        {
            string root = Path.Combine(_tempRoot, "jobs3");
            Directory.CreateDirectory(root);

            using var pkg = BuildPackage("out_job", "[]");
            string id = await JobPackageService.ImportJobPackageAsync(pkg, root);

            var manifest = JobManifest.Load(Path.Combine(root, id, "manifest.json"));
            Assert.Equal(AppConfig.GetDefaultOutputDirectory(), manifest!.OutputDirectory);
        }

        [Fact]
        public async Task Import_RejectsPackagesWithTooManyEntries()
        {
            var ms = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                for (int i = 0; i <= JobPackageService.MaxEntryCount; i++)
                {
                    archive.CreateEntry($"bomb_job/f{i}.txt", System.IO.Compression.CompressionLevel.NoCompression);
                }
            }
            ms.Position = 0;
            string root = Path.Combine(_tempRoot, "jobs4");
            Directory.CreateDirectory(root);

            await Assert.ThrowsAsync<InvalidJobPackageException>(() => JobPackageService.ImportJobPackageAsync(ms, root));
            Assert.Empty(Directory.GetDirectories(root));
        }

        // ---- AppConfig ----

        [Fact]
        public void Save_DoesNotLeaveCiphertextInLiveInstance()
        {
            string path = Path.Combine(_tempRoot, "config.json");
            var config = new AppConfig { ApiKey = "plain-key-123" };
            config.ProviderApiKeys["OpenAi"] = "provider-key-456";

            config.Save(path);

            Assert.Equal("plain-key-123", config.ApiKey);
            Assert.Equal("provider-key-456", config.ProviderApiKeys["OpenAi"]);
            Assert.Empty(Directory.GetFiles(_tempRoot, "*.tmp"));
        }

        [Fact]
        public void Reload_CopiesAllPersistedSettings()
        {
            string path = Path.Combine(_tempRoot, "config.json");
            var onDisk = new AppConfig { UpdateCheckIntervalHours = 42, StrictValidation = false, ModuleReviewEnabled = false, DismissedUpdateVersion = "9.9.9" };
            onDisk.Save(path);

            var live = AppConfig.Load(path);
            live.UpdateCheckIntervalHours = 1;
            live.ModuleReviewEnabled = true;
            live.DismissedUpdateVersion = null;

            live.Reload();

            Assert.Equal(42, live.UpdateCheckIntervalHours);
            Assert.False(live.ModuleReviewEnabled);
            Assert.Equal("9.9.9", live.DismissedUpdateVersion);
            Assert.Equal(path, live.LoadedFromPath);
        }

        [Fact]
        public void Load_BacksUpCorruptConfig()
        {
            string path = Path.Combine(_tempRoot, "config.json");
            File.WriteAllText(path, "{ this is not json");

            var config = AppConfig.Load(path);

            Assert.NotNull(config);
            Assert.Single(Directory.GetFiles(_tempRoot, "config.json.corrupt-*"));
        }

        [Fact]
        public void DefaultApiUrl_IsLmStudioLocalhost_NotDevWslIp()
        {
            string expected = LlmProviderHelper.GetDefaultApiUrl(LlmProvider.LmStudio);

            Assert.Equal(expected, new AppConfig().ApiUrl);
            Assert.Equal(expected, new CommandLineOptions().ApiUrl);
        }

        [Fact]
        public void DecryptionFailedMarker_PreservesOriginalCiphertextOnSave()
        {
            const string cipher = "enc:v1:AAAA-invalid-blob-AAAA";
            var config = new AppConfig();
            config.ProviderApiKeys["OpenAi"] = cipher;

            SecureField.UnprotectInPlace(config);
            Assert.True(SecureField.IsDecryptionFailed(config.ProviderApiKeys["OpenAi"]));

            // Al volver a cifrar, se restaura el ciphertext original (no el
            // marker cifrado).
            SecureField.ProtectInPlace(config);
            Assert.Equal(cipher, config.ProviderApiKeys["OpenAi"]);
        }

        [Fact]
        public void DecryptionFailedMarker_IsNeverUsedAsCredential()
        {
            var config = new AppConfig();
            config.ProviderApiKeys["OpenAi"] = "enc:v1:AAAA-invalid-blob-AAAA";
            SecureField.UnprotectInPlace(config);

            Assert.Null(SecureField.SanitizeForUse(config.ProviderApiKeys["OpenAi"]));
            Assert.Equal("real", SecureField.SanitizeForUse("real"));
        }

        // ---- Validadores / helpers ----

        [Theory]
        [InlineData("Español")]
        [InlineData("Português")]
        [InlineData("Brazilian-Portuguese")]
        public void ValidateJobSettings_AcceptsUnicodeLanguageNames(string language)
        {
            var config = new AppConfig { ApiUrl = "http://localhost:1234/v1", DefaultModel = "m", TargetLanguage = language, OutputDirectory = _tempRoot };

            var (isValid, error) = JobValidator.ValidateJobSettings(config, _tempRoot);

            Assert.True(isValid, error);
        }

        [Fact]
        public void ExportToMarkdown_DoesNotReorderCallerList()
        {
            var pages = new List<DocumentPageData>
            {
                new() { PageNumber = 2, OriginalText = "b" },
                new() { PageNumber = 1, OriginalText = "a" }
            };

            MarkdownWriter.ExportToMarkdown(Path.Combine(_tempRoot, "out.md"), "doc", "Spanish", pages, true);

            Assert.Equal(2, pages[0].PageNumber);
            Assert.Equal(1, pages[1].PageNumber);
        }

        [Theory]
        [InlineData("doc_original.md", true)]
        [InlineData("doc_Spanish.pdf", true)]
        [InlineData("doc2_Spanish.md", false)]
        [InlineData("document_original.md", false)]
        [InlineData("doc_French.md", false)]
        public void JobOutputFiles_MatchesOnlyExactOutputsOfTheDocument(string fileName, bool expected)
        {
            Assert.Equal(expected, JobOutputFiles.IsOutputOf(fileName, Path.Combine("src", "doc.pdf"), "Spanish"));
        }

        [Fact]
        public void ThinkTagParser_SplitsThoughtAndText()
        {
            var (text, thought) = ThinkTagParser.Split("<think> pienso </think>\nHola");

            Assert.Equal("Hola", text);
            Assert.Equal("pienso", thought);
            Assert.Equal("[OCR]:\na\n\n[Traducción]:\nb", ThinkTagParser.CombineThoughts("a", "b"));
        }

        [Theory]
        [InlineData("nightly", "1.0.0", false)]
        [InlineData("1.2.3", "nightly", true)]
        [InlineData("v99999999999.0.0", "1.0.0", false)]
        [InlineData("1.3.0", "1.2.9", true)]
        public void IsNewer_DoesNotThrow_OnNonNumericTags(string candidate, string current, bool expected)
        {
            Assert.Equal(expected, UpdateService.IsNewer(candidate.TrimStart('v'), current));
        }

        [Fact]
        public async Task DownloadInstallerAsync_FailsBeforeDownload_WhenNoDigest()
        {
            using var svc = new UpdateService("1.0.0", new System.Net.Http.HttpClient(new ThrowingHandler()));
            var info = new UpdateInfo
            {
                LatestVersion = "9.9.9",
                InstallerDownloadUrl = "https://github.com/setup.exe",
                Digest = string.Empty
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.DownloadInstallerAsync(info, ct: TestContext.Current.CancellationToken));
        }

        [Fact]
        public void ResolveProviderForUrl_NeverSendsKeyToUnknownHost()
        {
            var config = new AppConfig { Provider = "OpenAi", ApiUrl = "https://api.openai.com/v1", ApiKey = "sk-openai" };

            var (provider, key) = PromptHelperService.ResolveProviderForUrl(config, "https://evil.example.com/v1");
            Assert.Equal(LlmProvider.Custom, provider);
            Assert.Null(key);

            var (provider2, key2) = PromptHelperService.ResolveProviderForUrl(config, "https://api.openai.com/v1");
            Assert.Equal(LlmProvider.OpenAi, provider2);
            Assert.Equal("sk-openai", key2);
        }

        private sealed class ThrowingHandler : System.Net.Http.HttpMessageHandler
        {
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
                => throw new InvalidOperationException("No debería intentar descargar sin digest.");
        }

        /// <summary>Cliente LLM inerte para tests que no llegan a la red.</summary>
        private sealed class NullLlmClient : ILlmClient
        {
            public double Temperature { get; set; }
            public Task<string> SendTextRequestAsync(string systemPrompt, string userPrompt, string? modelName = null, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> SendVisionRequestAsync(string systemPrompt, string userPrompt, byte[] imageBytes, string mimeType, string? modelName = null, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<List<string>> GetAvailableModelsAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
            public Task<string> GetFirstLoadedModelAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task UnloadAllExceptAsync(string keepModelName, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<bool> UnloadModelAsync(string modelName, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(true);
            public void Dispose() { }
        }
    }
}
