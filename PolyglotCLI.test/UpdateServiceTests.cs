using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PolyglotCLI.Update;

namespace PolyglotCLI.test;

/// <summary>
/// Cubre las defensas de seguridad del UpdateService introducidas en v1.2.0:
/// whitelist de host, verificación SHA-256 del digest, re-verificación
/// pre-Process y rechazo de instaladores no verificados.
/// </summary>
public class UpdateServiceTests : IDisposable
{
    private readonly string _tempDir;

    public UpdateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"update_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* best effort */ }
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
    }

    [Fact]
    public void DownloadInstallerAsync_RejectsNonHttpsUrl()
    {
        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
            InstallerDownloadUrl = "http://github.com/x.exe", // HTTP, no HTTPS
            Digest = "sha256:abc"
        };
        using var svc = new UpdateService("1.0.0", new HttpClient());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            svc.DownloadInstallerAsync(info).GetAwaiter().GetResult());
        Assert.Contains("host de GitHub", ex.Message);
    }

    [Fact]
    public void DownloadInstallerAsync_RejectsNonGitHubHost()
    {
        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
            InstallerDownloadUrl = "https://evil.example.com/setup.exe",
            Digest = "sha256:abc"
        };
        using var svc = new UpdateService("1.0.0", new HttpClient());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            svc.DownloadInstallerAsync(info).GetAwaiter().GetResult());
        Assert.Contains("host de GitHub", ex.Message);
    }

    [Fact]
    public async Task DownloadInstallerAsync_AcceptsGitHubHost()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var digest = "sha256:" + ComputeSha256Hex(payload);
        var handler = new StubHandler(payload);
        using var http = new HttpClient(handler);
        using var svc = new UpdateService("1.0.0", http);

        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
            InstallerDownloadUrl = "https://objects.githubusercontent.com/setup.exe",
            Digest = digest
        };

        var path = await svc.DownloadInstallerAsync(info);
        try
        {
            Assert.True(File.Exists(path));
            Assert.Equal(payload, File.ReadAllBytes(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsWhenDigestMismatches()
    {
        var payload = Encoding.UTF8.GetBytes("the real payload");
        var wrongDigest = "sha256:" + ComputeSha256Hex(Encoding.UTF8.GetBytes("something else"));
        var handler = new StubHandler(payload);
        using var http = new HttpClient(handler);
        using var svc = new UpdateService("1.0.0", http);

        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
            InstallerDownloadUrl = "https://github.com/setup.exe",
            Digest = wrongDigest
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DownloadInstallerAsync(info));
        Assert.Contains("verificación SHA-256", ex.Message);
    }

    [Fact]
    public async Task DownloadInstallerAsync_AcceptsBareHexDigest()
    {
        // Por si en el futuro GitHub cambia el prefijo, aceptamos el hex
        // pelado (no "sha256:...").
        var payload = Encoding.UTF8.GetBytes("payload v2");
        var digest = ComputeSha256Hex(payload);
        var handler = new StubHandler(payload);
        using var http = new HttpClient(handler);
        using var svc = new UpdateService("1.0.0", http);

        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.0",
            InstallerDownloadUrl = "https://objects.githubusercontent.com/setup.exe",
            Digest = digest
        };

        var path = await svc.DownloadInstallerAsync(info);
        try { File.Delete(path); } catch { }
    }

    [Fact]
    public async Task DownloadInstallerAsync_SanitizesVersionInTempFilename()
    {
        // La versión viene del JSON de GitHub y no debe filtrarse al FS.
        var payload = new byte[] { 1, 2, 3 };
        var digest = "sha256:" + ComputeSha256Hex(payload);
        var handler = new StubHandler(payload);
        using var http = new HttpClient(handler);
        using var svc = new UpdateService("1.0.0", http);

        var info = new UpdateInfo
        {
            CurrentVersion = "1.0.0",
            LatestVersion = "../../etc/passwd", // intento de path traversal en la versión
            InstallerDownloadUrl = "https://github.com/x.exe",
            Digest = digest
        };

        var path = await svc.DownloadInstallerAsync(info);
        try
        {
            // Lo importante: el archivo resuelto debe caer estrictamente
            // dentro de %TEMP%/PolyglotCLI-Updates, no en un directorio
            // arbitrario del filesystem. La sanitización de la versión
            // reemplaza separadores y caracteres peligrosos con '_'; el
            // filename puede contener ".." como texto literal (es válido
            // en NTFS) pero el archivo en sí sigue dentro de tempDir.
            string tempUpdates = Path.Combine(Path.GetTempPath(), "PolyglotCLI-Updates");
            string fullPath = Path.GetFullPath(path);
            Assert.StartsWith(tempUpdates + Path.DirectorySeparatorChar, fullPath);
            string fileName = Path.GetFileName(path);
            // Tras la regex, no debe quedar ningún separador de path
            // (los '/' y '\' de la versión se reemplazan por '_').
            Assert.DoesNotContain("/", fileName);
            Assert.DoesNotContain("\\", fileName);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void LaunchSilentInstaller_RejectsUnverifiedPath()
    {
        // Aunque el .exe "exista", si nunca pasó por DownloadInstallerAsync
        // no debe ejecutarse: defensa contra llamadas externas al método.
        string fake = Path.Combine(_tempDir, "fake.exe");
        File.WriteAllBytes(fake, new byte[] { 1, 2, 3 });
        using var svc = new UpdateService("1.0.0", new HttpClient());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            svc.LaunchSilentInstaller(fake));
        Assert.Contains("no fue verificado", ex.Message);
    }

    [Fact]
    public void LaunchSilentInstaller_RejectsMissingFile()
    {
        using var svc = new UpdateService("1.0.0", new HttpClient());

        Assert.Throws<FileNotFoundException>(() =>
            svc.LaunchSilentInstaller(Path.Combine(_tempDir, "does-not-exist.exe")));
    }

    /// <summary>
    /// HttpMessageHandler que devuelve siempre un payload fijo, sin red.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        public StubHandler(byte[] payload) { _payload = payload; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_payload)
            };
            resp.Content.Headers.ContentLength = _payload.Length;
            return Task.FromResult(resp);
        }
    }
}
