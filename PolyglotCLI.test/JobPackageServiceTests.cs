using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using PolyglotCLI;

namespace PolyglotCLI.test
{
    public class JobPackageServiceTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _jobsRoot;

        public JobPackageServiceTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"polyglot-cli-jpkg-tests-{Guid.NewGuid():N}");
            _jobsRoot = Path.Combine(_tempRoot, "jobs");
            Directory.CreateDirectory(_jobsRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                try { Directory.Delete(_tempRoot, recursive: true); } catch { }
            }
        }

        private string CreateSyntheticJob(string jobId)
        {
            string jobDir = Path.Combine(_jobsRoot, jobId);
            Directory.CreateDirectory(jobDir);
            Directory.CreateDirectory(Path.Combine(jobDir, "data"));
            Directory.CreateDirectory(Path.Combine(jobDir, "sources"));
            Directory.CreateDirectory(Path.Combine(jobDir, "outputs"));

            // manifest.json + config.json + data/*.json
            File.WriteAllText(
                Path.Combine(jobDir, "manifest.json"),
                $"{{\"JobId\":\"{jobId}\",\"Status\":\"Completed\",\"CreatedAt\":\"2026-01-01T00:00:00Z\",\"LastUpdatedAt\":\"2026-01-01T00:00:00Z\",\"TargetLanguage\":\"Spanish\",\"Mode\":\"text\",\"OutputDirectory\":\"out\",\"PageRange\":\"all\",\"Transcribe\":true,\"Translate\":true,\"Verify\":false,\"GenerateDoc\":false,\"Files\":[]}}");

            File.WriteAllText(
                Path.Combine(jobDir, "config.json"),
                "{\"ApiUrl\":\"http://localhost:11434\",\"TargetLanguage\":\"Spanish\"}");

            File.WriteAllText(
                Path.Combine(jobDir, "data", "document_data.json"),
                "[{\"PageNumber\":1,\"OriginalText\":\"Hello\",\"TranslatedText\":\"Hola\",\"ReviewedText\":\"Hola\"}]");

            File.WriteAllText(
                Path.Combine(jobDir, "sources", "document.pdf"),
                "%PDF-1.4 sample");

            return jobDir;
        }

        [Fact]
        public void ExportJobPackage_CreatesValidZipWithManifest()
        {
            string jobId = "20260722_143935";
            string jobDir = CreateSyntheticJob(jobId);

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            Assert.True(output.Length > 0, "Exported zip must have content");

            using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Read);
            Assert.Contains(archive.Entries, e => e.FullName == $"{jobId}/manifest.json");
            Assert.Contains(archive.Entries, e => e.FullName == $"{jobId}/data/document_data.json");
        }

        [Fact]
        public void ExportJobPackage_NoPACKAGE_NOTES_WhenCompleted()
        {
            string jobId = "completed_job";
            string jobDir = CreateSyntheticJob(jobId);

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Read);
            Assert.DoesNotContain(archive.Entries, e => e.FullName.EndsWith("PACKAGE_NOTES.txt", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ExportJobPackage_AddsPACKAGE_NOTES_WhenInProgress()
        {
            string jobId = "running_job";
            string jobDir = CreateSyntheticJob(jobId);

            var manifestPath = Path.Combine(jobDir, "manifest.json");
            var manifestJson = File.ReadAllText(manifestPath).Replace("\"Completed\"", "\"InProgress\"");
            File.WriteAllText(manifestPath, manifestJson);

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Read);
            Assert.Contains(archive.Entries, e => e.FullName == $"{jobId}/PACKAGE_NOTES.txt");
        }

        [Fact]
        public void ExportImport_RoundTrip_PreservesManifest()
        {
            string jobId = "roundtrip_job";
            string jobDir = CreateSyntheticJob(jobId);

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            // Importar en una carpeta vacía separada para que no haya renombrado
            // por conflicto de JobId.
            string targetRoot = Path.Combine(_tempRoot, "roundtrip-target");
            Directory.CreateDirectory(targetRoot);

            string restoredId = JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot).GetAwaiter().GetResult();

            Assert.Equal(jobId, restoredId);
            string restoredDir = Path.Combine(targetRoot, restoredId);
            Assert.True(File.Exists(Path.Combine(restoredDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(restoredDir, "data", "document_data.json")));
        }

        [Fact]
        public void ImportJobPackageAsync_RenamesOnConflict()
        {
            string jobId = "conflict_job";
            string jobDir = CreateSyntheticJob(jobId);

            using var output = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, output);
            output.Position = 0;

            // Carpeta destino separada. Importamos el mismo zip dos veces: la
            // segunda debe producir el sufijo _imported_<timestamp>.
            string targetRoot = Path.Combine(_tempRoot, "conflict-target");
            Directory.CreateDirectory(targetRoot);

            string firstId = JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot).GetAwaiter().GetResult();
            Assert.Equal(jobId, firstId);
            Assert.True(Directory.Exists(Path.Combine(targetRoot, jobId)));

            string secondId = JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot).GetAwaiter().GetResult();

            Assert.NotEqual(jobId, secondId);
            Assert.StartsWith(jobId, secondId);
            Assert.Contains("_imported_", secondId);
        }

        [Fact]
        public async Task ImportJobPackageAsync_RejectsEmptyZip()
        {
            string emptyZipPath = Path.Combine(_tempRoot, "empty.zip");
            using (var fs = File.Create(emptyZipPath))
            {
                using var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true);
                // empty
            }

            await using var input = File.OpenRead(emptyZipPath);
            var ex = await Assert.ThrowsAsync<InvalidJobPackageException>(async () =>
                await JobPackageService.ImportJobPackageAsync(input, _jobsRoot));
            Assert.Contains("vacío", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ImportJobPackageAsync_RejectsNonZipFile()
        {
            string notZipPath = Path.Combine(_tempRoot, "not-a-zip.txt");
            File.WriteAllText(notZipPath, "This is plain text, not a zip file at all.");

            await using var input = File.OpenRead(notZipPath);
            var ex = await Assert.ThrowsAsync<InvalidJobPackageException>(async () =>
                await JobPackageService.ImportJobPackageAsync(input, _jobsRoot));
            Assert.Contains("ZIP", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ImportJobPackageAsync_ReportsFilesFound_WhenManifestMissing()
        {
            // Create a zip that has the right top-level prefix but no manifest.json
            string zipPath = Path.Combine(_tempRoot, "no-manifest.zip");
            using (var fs = File.Create(zipPath))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("orphan_job/readme.txt", System.IO.Compression.CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open());
                writer.Write("just a readme");
            }

            await using var input = File.OpenRead(zipPath);
            var ex = await Assert.ThrowsAsync<InvalidJobPackageException>(async () =>
                await JobPackageService.ImportJobPackageAsync(input, _jobsRoot));
            Assert.Contains("manifest.json", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
