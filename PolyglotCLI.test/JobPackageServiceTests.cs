using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
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
        public async Task ExportImport_RoundTrip_PreservesManifest()
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

            string restoredId = await JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot);

            Assert.Equal(jobId, restoredId);
            string restoredDir = Path.Combine(targetRoot, restoredId);
            Assert.True(File.Exists(Path.Combine(restoredDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(restoredDir, "data", "document_data.json")));
        }

        [Fact]
        public async Task ImportJobPackageAsync_RenamesOnConflict()
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

            string firstId = await JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot);
            Assert.Equal(jobId, firstId);
            Assert.True(Directory.Exists(Path.Combine(targetRoot, jobId)));

            string secondId = await JobPackageService.ImportJobPackageAsync(new MemoryStream(output.ToArray()), targetRoot);

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

        // ---- Portabilidad del SourceFilePath entre máquinas ----
        // El manifest exportado guarda el SourceFilePath absoluto del
        // equipo de origen (la ruta donde el usuario tenía su archivo
        // original al crear el job, p.ej. D:\mis_docs\doc.pdf o
        // C:\Users\...\AppData\Roaming\PolyglotCLI\jobs\...). Esas rutas
        // no existen en el equipo que importa y no son operativas: el
        // sistema siempre trabaja sobre las copias depositadas en
        // <jobDir>\sources\, que es lo único que vino dentro del .zpg.
        // Al importar reescribimos cada SourceFilePath a la ruta absoluta
        // de esa copia local, sin importar qué valor traía el manifest
        // (sea absoluto, relativo, con "..", etc.). La seguridad es
        // implícita: el destino usa NormalizedFileName (saneado a
        // [a-zA-Z0-9_\-\.]) y siempre cae dentro de sources/, así que
        // un manifest hostil no puede escapar del job.

        [Fact]
        public async Task ImportJobPackageAsync_RewritesAbsoluteSourceFilePath()
        {
            // SourceFilePath absoluto del equipo original: el caso típico
            // que rompe el round-trip entre máquinas. Debe importarse
            // bien, reescribiendo la ruta a la copia local en sources/.
            string manifest = @"{
                ""JobId"": ""abs_path_job"",
                ""Status"": ""Completed"",
                ""CreatedAt"": ""2026-01-01T00:00:00Z"",
                ""LastUpdatedAt"": ""2026-01-01T00:00:00Z"",
                ""TargetLanguage"": ""Spanish"",
                ""Mode"": ""text"",
                ""OutputDirectory"": ""out"",
                ""PageRange"": ""all"",
                ""Transcribe"": true,
                ""Translate"": true,
                ""Verify"": false,
                ""GenerateDoc"": false,
                ""Files"": [
                    {
                        ""SourceFilePath"": ""C:\\Users\\Victim\\Documents\\secrets.pdf"",
                        ""OriginalFileName"": ""secrets.pdf"",
                        ""NormalizedFileName"": ""secrets.pdf"",
                        ""CopiedFilePath"": ""C:\\Users\\Victim\\Documents\\secrets.pdf"",
                        ""TargetLanguage"": ""Spanish"",
                        ""Completed"": true,
                        ""Pages"": []
                    }
                ]
            }";

            string zipPath = Path.Combine(_tempRoot, "abs-path.zip");
            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("abs_path_job/manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(manifest);
                }
                var data = archive.CreateEntry("abs_path_job/data/secrets_data.json", CompressionLevel.Optimal);
                using (var dw = new StreamWriter(data.Open()))
                {
                    dw.Write("[]");
                }
                // Incluimos el source reescrito para que un re-proceso
                // posterior pueda abrirlo desde el árbol extraído.
                var src = archive.CreateEntry("abs_path_job/sources/secrets.pdf", CompressionLevel.Optimal);
                using var sw = new StreamWriter(src.Open());
                sw.Write("%PDF-1.4 sample");
            }

            // Destino separado para no chocar con un import previo.
            string targetRoot = Path.Combine(_tempRoot, "abs-path-target");
            Directory.CreateDirectory(targetRoot);

            await using var input = File.OpenRead(zipPath);
            string restoredId = await JobPackageService.ImportJobPackageAsync(input, targetRoot);

            Assert.Equal("abs_path_job", restoredId);
            string restoredDir = Path.Combine(targetRoot, restoredId);
            string restoredManifest = Path.Combine(restoredDir, "manifest.json");
            Assert.True(File.Exists(restoredManifest));

            // El manifest reescrito debe tener SourceFilePath apuntando a
            // la copia local dentro del job, NUNCA a la ruta absoluta
            // original. Esta es la invariante que hace que el import
            // funcione en otra máquina (incluso sin D: o sin el usuario
            // original) y que un re-proceso posterior encuentre el archivo.
            string json = File.ReadAllText(restoredManifest);
            using var doc = JsonDocument.Parse(json);
            var files = doc.RootElement.GetProperty("Files");
            Assert.Equal(1, files.GetArrayLength());
            string? rewritten = files[0].GetProperty("SourceFilePath").GetString();
            string expected = Path.Combine(restoredDir, "sources", "secrets.pdf");
            Assert.Equal(expected, rewritten);
            Assert.False(rewritten!.Contains("C:\\Users\\Victim", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ImportJobPackageAsync_RewritesStaleRoamingPath()
        {
            // Caso real del error reportado: el manifest trae el path
            // completo al árbol de jobs del usuario, p.ej.
            // %AppData%\PolyglotCLI\jobs\...\sources\doc.pdf. En la PC
            // de origen ese path existe, pero al importarlo en otra PC
            // (que ni siquiera tiene esa unidad) no resuelve. El fix:
            // reescribir siempre a la copia local dentro del job.
            string stalePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PolyglotCLI", "jobs", "stale_job", "sources", "doc.pdf");

            string manifestTemplate = @"{
                ""JobId"": ""stale_job"",
                ""Status"": ""Completed"",
                ""CreatedAt"": ""2026-01-01T00:00:00Z"",
                ""LastUpdatedAt"": ""2026-01-01T00:00:00Z"",
                ""TargetLanguage"": ""Spanish"",
                ""Mode"": ""text"",
                ""OutputDirectory"": ""out"",
                ""PageRange"": ""all"",
                ""Transcribe"": true,
                ""Translate"": true,
                ""Verify"": false,
                ""GenerateDoc"": false,
                ""Files"": [
                    {
                        ""SourceFilePath"": ""__STALE__"",
                        ""OriginalFileName"": ""doc.pdf"",
                        ""NormalizedFileName"": ""doc.pdf"",
                        ""CopiedFilePath"": ""__STALE__"",
                        ""TargetLanguage"": ""Spanish"",
                        ""Completed"": true,
                        ""Pages"": []
                    }
                ]
            }";
            string manifest = manifestTemplate.Replace(
                "__STALE__",
                stalePath.Replace("\\", "\\\\"));

            string zipPath = Path.Combine(_tempRoot, "stale.zip");
            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("stale_job/manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(manifest);
                }
            }

            string targetRoot = Path.Combine(_tempRoot, "stale-target");
            Directory.CreateDirectory(targetRoot);

            await using var input = File.OpenRead(zipPath);
            string restoredId = await JobPackageService.ImportJobPackageAsync(input, targetRoot);

            Assert.Equal("stale_job", restoredId);
            string json = File.ReadAllText(Path.Combine(targetRoot, restoredId, "manifest.json"));
            using var doc = JsonDocument.Parse(json);
            string? rewritten = doc.RootElement.GetProperty("Files")[0]
                .GetProperty("SourceFilePath").GetString();
            string expected = Path.Combine(targetRoot, restoredId, "sources", "doc.pdf");
            Assert.Equal(expected, rewritten);
        }

        [Fact]
        public async Task ImportJobPackageAsync_IgnoresTraversalInSourceFilePath()
        {
            // Aunque el manifest intente path traversal en SourceFilePath,
            // el rewrite usa NormalizedFileName (saneado) y ancla el
            // destino en <finalDir>/sources/. El resultado es siempre una
            // ruta dentro del job, no en C:\Windows\System32 ni en
            // ningún lado fuera del paquete. No hay nada que rechazar:
            // el import funciona y el sistema nunca intenta abrir el
            // path original.
            string manifest = @"{
                ""JobId"": ""traversal_job"",
                ""Status"": ""Completed"",
                ""CreatedAt"": ""2026-01-01T00:00:00Z"",
                ""LastUpdatedAt"": ""2026-01-01T00:00:00Z"",
                ""TargetLanguage"": ""Spanish"",
                ""Mode"": ""text"",
                ""OutputDirectory"": ""out"",
                ""PageRange"": ""all"",
                ""Transcribe"": true,
                ""Translate"": true,
                ""Verify"": false,
                ""GenerateDoc"": false,
                ""Files"": [
                    {
                        ""SourceFilePath"": ""..\\..\\..\\Windows\\System32\\drivers\\etc\\hosts"",
                        ""OriginalFileName"": ""hosts"",
                        ""NormalizedFileName"": ""hosts"",
                        ""CopiedFilePath"": ""x"",
                        ""TargetLanguage"": ""Spanish"",
                        ""Completed"": true,
                        ""Pages"": []
                    }
                ]
            }";

            string zipPath = Path.Combine(_tempRoot, "traversal.zip");
            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("traversal_job/manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(manifest);
                }
                var data = archive.CreateEntry("traversal_job/data/hosts_data.json", CompressionLevel.Optimal);
                using (var dw = new StreamWriter(data.Open()))
                {
                    dw.Write("[]");
                }
            }

            string targetRoot = Path.Combine(_tempRoot, "traversal-target");
            Directory.CreateDirectory(targetRoot);

            await using var input = File.OpenRead(zipPath);
            string restoredId = await JobPackageService.ImportJobPackageAsync(input, targetRoot);

            Assert.Equal("traversal_job", restoredId);
            string restoredDir = Path.Combine(targetRoot, restoredId);
            string json = File.ReadAllText(Path.Combine(restoredDir, "manifest.json"));
            using var doc = JsonDocument.Parse(json);
            string? rewritten = doc.RootElement.GetProperty("Files")[0]
                .GetProperty("SourceFilePath").GetString();
            // El rewrite va a la copia local, NUNCA al path traversal.
            string expected = Path.Combine(restoredDir, "sources", "hosts");
            Assert.Equal(expected, rewritten);
            Assert.False(rewritten!.Contains("..", StringComparison.Ordinal));
            Assert.False(rewritten.Contains("Windows", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ImportJobPackageAsync_AcceptsManifestWithRelativeSourceFilePath()
        {
            // Path relativo al top-level prefix está OK: es el caso normal
            // que ExportJobPackage produce.
            string manifest = @"{
                ""JobId"": ""relpath_job"",
                ""Status"": ""Completed"",
                ""CreatedAt"": ""2026-01-01T00:00:00Z"",
                ""LastUpdatedAt"": ""2026-01-01T00:00:00Z"",
                ""TargetLanguage"": ""Spanish"",
                ""Mode"": ""text"",
                ""OutputDirectory"": ""out"",
                ""PageRange"": ""all"",
                ""Transcribe"": true,
                ""Translate"": true,
                ""Verify"": false,
                ""GenerateDoc"": false,
                ""Files"": [
                    {
                        ""SourceFilePath"": ""sources\\relpath.pdf"",
                        ""OriginalFileName"": ""relpath.pdf"",
                        ""NormalizedFileName"": ""relpath.pdf"",
                        ""CopiedFilePath"": ""sources\\relpath.pdf"",
                        ""TargetLanguage"": ""Spanish"",
                        ""Completed"": true,
                        ""Pages"": []
                    }
                ]
            }";

            string zipPath = Path.Combine(_tempRoot, "relpath.zip");
            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("relpath_job/manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(manifest);
                }
                var data = archive.CreateEntry("relpath_job/data/relpath_data.json", CompressionLevel.Optimal);
                using (var dw = new StreamWriter(data.Open()))
                {
                    dw.Write("[]");
                }
            }

            string targetRoot = Path.Combine(_tempRoot, "relpath-target");
            Directory.CreateDirectory(targetRoot);

            await using var input = File.OpenRead(zipPath);
            string restoredId = await JobPackageService.ImportJobPackageAsync(input, targetRoot);
            Assert.Equal("relpath_job", restoredId);

            // Misma invariante que en los otros casos: el manifest
            // persistido tiene SourceFilePath apuntando a la copia local
            // del job, no al path relativo "sources/relpath.pdf" que
            // vinía en el .zpg.
            string json = File.ReadAllText(Path.Combine(targetRoot, restoredId, "manifest.json"));
            using var doc = JsonDocument.Parse(json);
            string? rewritten = doc.RootElement.GetProperty("Files")[0]
                .GetProperty("SourceFilePath").GetString();
            string expected = Path.Combine(targetRoot, restoredId, "sources", "relpath.pdf");
            Assert.Equal(expected, rewritten);
        }
    }
}
