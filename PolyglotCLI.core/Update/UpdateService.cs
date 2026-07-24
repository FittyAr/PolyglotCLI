using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PolyglotCLI.Update
{
    /// <summary>
    /// Servicio de auto-actualización para instalaciones Inno Setup
    /// (.exe). <b>No hace nada en MSIX</b>: el Store se encarga.
    ///
    /// Flujo:
    ///   1. <see cref="CheckForUpdateAsync"/> consulta
    ///      <c>https://api.github.com/repos/FittyAr/PolyglotCLI/releases/latest</c>
    ///      y compara el <c>tag_name</c> con la versión instalada.
    ///   2. Si hay update, <see cref="DownloadInstallerAsync"/> baja el
    ///      .exe de la release a un temporal.
    ///   3. <see cref="LaunchSilentInstaller"/> arranca el instalador
    ///      con los flags silenciosos de Inno Setup
    ///      (<c>/VERYSILENT /SP- /CLOSEAPPLICATIONS /NORESTART</c>), que
    ///      respeta la selección de componentes guardada en el registro.
    ///   4. El proceso de PolyglotCLI debe cerrarse
    ///      (<c>/CLOSEAPPLICATIONS</c> lo cierra automáticamente).
    /// </summary>
    public sealed class UpdateService
    {
        /// <summary>
        /// Repositorio desde el que se consulta la última release.
        /// Centralizado aquí para que la UI y la lógica compartan la
        /// misma URL.
        /// </summary>
        public const string GitHubOwner = "FittyAr";
        public const string GitHubRepo  = "PolyglotCLI";

        /// <summary>
        /// Timeout prudente para la consulta al endpoint de GitHub
        /// (release latest). 8s cubre conexiones lentas sin colgar la UI.
        /// </summary>
        public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(8);

        private readonly HttpClient _http;
        private readonly string _currentVersion;

        public UpdateService(string currentVersion, HttpClient? http = null)
        {
            _currentVersion = (currentVersion ?? string.Empty).Trim();
            if (_currentVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                _currentVersion = _currentVersion[1..];
            }
            _http = http ?? new HttpClient { Timeout = HttpTimeout };
        }

        /// <summary>
        /// Consulta la última release y devuelve un <see cref="UpdateInfo"/>
        /// con la comparación ya resuelta. Nunca lanza excepciones: los
        /// errores se empaquetan en <see cref="UpdateInfo.Failed"/>.
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
        {
            string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("PolyglotCLI-Updater", _currentVersion));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            try
            {
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return UpdateInfo.Failed(_currentVersion,
                        "Rate limit de GitHub alcanzado. Intenta de nuevo en una hora.");
                }
                resp.EnsureSuccessStatusCode();

                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                var root = doc.RootElement;

                string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                string latest = StripVPrefix(tag);

                // Buscar el asset del instalador: PolyglotCLI-*x64-setup.exe
                string installerUrl = string.Empty;
                long installerSize = 0;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                        if (name.EndsWith("-x64-setup.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installerUrl = asset.TryGetProperty("browser_download_url", out var b)
                                ? b.GetString() ?? string.Empty
                                : string.Empty;
                            installerSize = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz)
                                ? sz : 0;
                            break;
                        }
                    }
                }

                string notes = root.TryGetProperty("body", out var b2) ? b2.GetString() ?? string.Empty : string.Empty;
                DateTime? published = null;
                if (root.TryGetProperty("published_at", out var pa) &&
                    DateTime.TryParse(pa.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var dt))
                {
                    published = dt;
                }

                bool isNewer = IsNewer(latest, _currentVersion);

                return new UpdateInfo
                {
                    CurrentVersion = _currentVersion,
                    LatestVersion = latest,
                    InstallerDownloadUrl = installerUrl,
                    InstallerSizeBytes = installerSize,
                    ReleaseNotes = notes,
                    PublishedAt = published,
                    IsUpdateAvailable = isNewer && !string.IsNullOrEmpty(installerUrl),
                    CheckSucceeded = true
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return UpdateInfo.Failed(_currentVersion, ex.Message);
            }
        }

        /// <summary>
        /// Descarga el instalador a un archivo temporal único. Devuelve la
        /// ruta completa del archivo. El llamador es responsable de
        /// eliminarlo tras usarlo (o dejar que Windows limpie %TEMP%).
        /// </summary>
        public async Task<string> DownloadInstallerAsync(
            UpdateInfo info,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            if (info is null) throw new ArgumentNullException(nameof(info));
            if (string.IsNullOrEmpty(info.InstallerDownloadUrl))
                throw new InvalidOperationException("La release no incluye un instalador .exe.");

            string tempDir = Path.Combine(Path.GetTempPath(), "PolyglotCLI-Updates");
            Directory.CreateDirectory(tempDir);

            string fileName = $"PolyglotCLI-{info.LatestVersion}-setup-{Guid.NewGuid():N}.exe";
            string outPath = Path.Combine(tempDir, fileName);

            using var req = new HttpRequestMessage(HttpMethod.Get, info.InstallerDownloadUrl);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("PolyglotCLI-Updater", _currentVersion));

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength;
            await using var net = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            byte[] buf = new byte[81920];
            long read = 0;
            int n;
            int lastPct = -1;
            while ((n = await net.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total is long t && t > 0 && progress is not null)
                {
                    int pct = (int)Math.Min(99, read * 100 / t);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        progress.Report(pct / 100.0);
                    }
                }
            }
            progress?.Report(1.0);
            return outPath;
        }

        /// <summary>
        /// Lanza el instalador recién descargado en modo silencioso.
        /// Inno Setup detecta la instalación existente por AppId, usa la
        /// misma ruta y mantiene la selección de componentes previa
        /// (server, desktop o ambos). El flag <c>/CLOSEAPPLICATIONS</c>
        /// cierra PolyglotCLI antes de actualizar.
        ///
        /// <para>
        /// <b>Importante</b>: la elevación UAC aparecerá aunque el flag
        /// sea <c>/VERYSILENT</c> porque el instalador se ejecuta en modo
        /// administrador (<c>PrivilegesRequired=admin</c> en el .iss).
        /// Esto es intencional: la instalación de PolyglotCLI vive en
        /// <c>%ProgramFiles%</c> y requiere privilegios.
        /// </para>
        /// </summary>
        /// <returns>El <see cref="Process"/> lanzado (no se espera a que
        /// termine; debe ser el proceso de PolyglotCLI el que termine con
        /// <see cref="Environment.Exit"/>, no el instalador).</returns>
        public Process LaunchSilentInstaller(string installerPath)
        {
            if (string.IsNullOrEmpty(installerPath))
                throw new ArgumentException("Ruta vacía.", nameof(installerPath));
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Instalador no encontrado.", installerPath);

            // Inno Setup: /VERYSILENT /SP- /CLOSEAPPLICATIONS /NORESTART
            // - /VERYSILENT: instala sin mostrar el wizard
            // - /SP-: suprime la página "¿Desea instalar?"
            // - /CLOSEAPPLICATIONS: cierra PolyglotCLI antes de actualizar
            // - /NORESTART: no fuerza reinicio (lo hace el usuario si lo necesita)
            const string args = "/VERYSILENT /SP- /CLOSEAPPLICATIONS /NORESTART /LOG";

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = args,
                UseShellExecute = true,             // necesario para que aparezca el UAC
                Verb = "runas",                     // fuerza elevación admin
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty
            };
            return Process.Start(psi) ?? throw new InvalidOperationException(
                "No se pudo iniciar el instalador (Process.Start devolvió null).");
        }

        // --- helpers ---

        private static string StripVPrefix(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? s[1..] : s;
        }

        /// <summary>
        /// Compara dos versiones "x.y.z" (ignorando sufijos tipo
        /// "-rc1" o "+meta"). Devuelve <c>true</c> cuando
        /// <paramref name="candidate"/> es estrictamente mayor que
        /// <paramref name="current"/>.
        /// </summary>
        internal static bool IsNewer(string candidate, string current)
        {
            if (string.IsNullOrEmpty(candidate)) return false;
            if (string.IsNullOrEmpty(current))   return true;

            static int[] Parts(string v)
            {
                var m = Regex.Match(v, @"^\s*(\d+)(?:\.(\d+))?(?:\.(\d+))?");
                if (!m.Success) return new[] { 0 };
                int[] p = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    p[i] = m.Groups[i + 1].Success ? int.Parse(m.Groups[i + 1].Value) : 0;
                }
                return p;
            }

            int[] a = Parts(candidate);
            int[] b = Parts(current);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
            return false;
        }
    }
}
