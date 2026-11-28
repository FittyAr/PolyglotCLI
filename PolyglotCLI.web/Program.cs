using PolyglotCLI.web.Components;
using PolyglotCLI.web.Localization;
using PolyglotCLI.web.Services;
using PolyglotCLI.web.Services.JobDetails;
using Radzen;
using BlazorPanzoom;
using PolyglotCLI;
using PolyglotCLI.Update;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace PolyglotCLI.web
{
    public static class Program
    {
        /// <summary>URL local única donde escucha el servidor (solo loopback).</summary>
        public const string LocalBaseUrl = "http://localhost:5000";

        /// <summary>
        /// Tamaño máximo aceptado para importar un paquete .zpg (2 GB),
        /// coherente con el límite que muestra ImportJobDialog.
        /// </summary>
        private const long MaxImportPackageBytes = 2L * 1024 * 1024 * 1024;

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Inicializar la configuracion y el logger
            var config = AppConfig.Load();
            AppLogger.Initialize(config);
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(new ApplicationMode(isWebMode: true));

            // Idioma de la interfaz (config o, si está vacío, el de Windows).
            Loc.ApplyLanguage(config.UiLanguage);
            builder.Services.AddSingleton(Loc.Instance);

            // Forzar la URL a localhost:5000
            builder.WebHost.UseUrls(LocalBaseUrl);

            // Defensa contra DNS rebinding: aunque Kestrel solo escucha en
            // loopback, una web maliciosa puede re-resolver su dominio a
            // 127.0.0.1 y hablar con el server como "same-origin". Filtrar el
            // header Host deja pasar únicamente nombres locales.
            builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(o =>
            {
                o.AllowedHosts = new[] { "localhost", "127.0.0.1", "[::1]" };
            });

            // Registrar componentes de Razor y Radzen
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents(options => {
                    // Stack traces al navegador solo en desarrollo.
                    options.DetailedErrors = builder.Environment.IsDevelopment();
                })
                .AddHubOptions(options => {
                    // El visor de página transmite imágenes base64 sobre SignalR;
                    // ampliar el límite por encima del valor por defecto (32KB).
                    // 32MB soporta miniaturas razonables sin abrir la puerta a
                    // un atacante que bombardee el hub con payloads grandes.
                    options.MaximumReceiveMessageSize = 32 * 1024 * 1024; // 32MB
                });
            builder.Services.AddRadzenComponents();
            builder.Services.AddBlazorPanzoomServices();

            // HttpClient para que componentes (p.ej. el importador de .zpg) suban archivos directamente
            // al endpoint HTTP sin pasar por SignalR, evitando el límite de tamaño del hub.
            // Timeout infinito: la subida de un .zpg grande puede superar los
            // 100 s por defecto; la cancelación la controla el diálogo.
            builder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri(LocalBaseUrl),
                Timeout = Timeout.InfiniteTimeSpan
            });

            // Servicio de empaquetado de trabajos, separado por modo de ejecución
            // (Web usa HTTP endpoints; MAUI usa file pickers nativos).
            builder.Services.AddScoped<IJobPackageHost, WebJobPackageHost>();
            builder.Services.AddScoped<IFolderPickerService, WebFolderPickerService>();
            builder.Services.AddScoped<IJobArtifactsService, JobArtifactsService>();
            builder.Services.AddScoped<IJobPageVerifierService, JobPageVerifierService>();
            builder.Services.AddScoped<IJobPageEditService, JobPageEditService>();
            builder.Services.AddScoped<IJobPageReprocessService, JobPageReprocessService>();

            // Servicio de auto-actualización (background). Solo activo en
            // instalaciones Inno Setup / .exe; en MSIX no se registra
            // ningún chequeo porque lo gestiona Microsoft Store.
            if (InstallEnvironment.CanSelfUpdate)
            {
                builder.Services.AddHostedService<UpdateHostedService>();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
            }
            // Nota: el server escucha en http://localhost:5000 (UseUrls arriba),
            // por lo que UseHsts y UseHttpsRedirection no surten efecto. Si en
            // el futuro se expone HTTPS, hay que reactivar ambos.
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();

            // UseStaticFiles con un proveedor de MIME types extendido:
            // por defecto el middleware rechaza archivos sin extensión
            // (p.ej. LICENSE) porque no les puede asignar Content-Type.
            // El AboutConfigTab enlaza LICENSE, README.md, y
            // docs/architecture.svg, así que necesitamos servir todos
            // los archivos de wwwroot/ sin importar la extensión.
            var staticContentTypes = new FileExtensionContentTypeProvider();
            // Asegurar que .md se sirva como text/markdown
            staticContentTypes.Mappings[".md"] = "text/markdown; charset=utf-8";
            // Servir archivos sin extensión reconocida (LICENSE, etc.) como
            // text/plain para que el navegador los pueda mostrar.
            var staticFileOptions = new StaticFileOptions
            {
                ContentTypeProvider = staticContentTypes,
                ServeUnknownFileTypes = true,
                DefaultContentType = "text/plain; charset=utf-8"
            };
            app.UseStaticFiles(staticFileOptions);
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // -------- Endpoints de exportación / importación de trabajos (.zpg) --------
            // GET /api/jobs/{jobId}/package → descarga el trabajo como .zpg
            app.MapGet("/api/jobs/{jobId}/package", (string jobId) =>
            {
                try
                {
                    // Whitelist positivo: solo letras, dígitos, guion bajo/medio/punto.
                    // Antes se usaba Contains("..")/Contains('/')/Contains('\\'), que
                    // deja pasar caracteres como NUL, o secuencias como "....//" que
                    // algunos normalizadores de rutas toleran.
                    if (string.IsNullOrWhiteSpace(jobId) || jobId.Length > 128 ||
                        !System.Text.RegularExpressions.Regex.IsMatch(jobId, @"^[A-Za-z0-9._-]+$"))
                    {
                        return Results.BadRequest(new { error = "Invalid jobId" });
                    }

                    string jobsRoot = TranslationOrchestrator.GetJobsDirectory();
                    string jobDir = Path.Combine(jobsRoot, jobId);
                    if (!Directory.Exists(jobDir))
                    {
                        return Results.NotFound(new { error = $"Job '{jobId}' not found" });
                    }

                    // Se escribe el zip a un archivo temporal (no a memoria) para
                    // no retener en RAM trabajos de varios GB. El archivo se
                    // borra automáticamente al cerrarse el stream.
                    string tempPath = Path.Combine(Path.GetTempPath(), $"polyglot-export-{Guid.NewGuid():N}.zpg");
                    var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                        bufferSize: 81920, FileOptions.DeleteOnClose);
                    try
                    {
                        JobPackageService.ExportJobPackage(jobDir, stream);
                        stream.Position = 0;
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }

                    string fileName = $"{jobId}{JobPackageService.PackageExtension}";
                    return Results.File(stream, JobPackageService.PackageMimeType, fileName);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to export job package '{jobId}'", ex);
                    return Results.Problem(detail: ex.Message, statusCode: 500, title: "Export failed");
                }
            });

            // POST /api/jobs/import  (multipart/form-data con campo "file")
            // Devuelve { jobId = "<nuevo JobId efectivo>" }
            // CSRF: aceptamos sólo dos clases de cliente:
            //   (a) navegador same-origin (Origin/Referer contra http://localhost:5000)
            //   (b) cliente PolyglotCLI de confianza que envía el header
            //       X-Polyglot-Client con un valor whitelisted.
            //   Un atacante cross-origin no puede (a) ni setear el header
            //   custom en (b) sin disparar preflight CORS. Mantenemos el
            //   endpoint sin antiforgery del middleware para no romper el
            //   Blazor HttpClient interno, pero este filtro cumple el mismo
            //   rol sin requerir cookie/session.
            app.MapPost("/api/jobs/import", async (HttpRequest req) =>
            {
                if (!IsTrustedImportClient(req))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                try
                {
                    if (!req.HasFormContentType)
                    {
                        return Results.BadRequest(new { error = Loc.T("Host.MultipartRequired") });
                    }

                    var form = await req.ReadFormAsync();
                    var file = form.Files["file"];
                    if (file == null || file.Length == 0)
                    {
                        return Results.BadRequest(new { error = Loc.T("Host.NoFileProvided") });
                    }

                    string jobsRoot = TranslationOrchestrator.GetJobsDirectory();
                    string newJobId;
                    using (var src = file.OpenReadStream())
                    {
                        newJobId = await JobPackageService.ImportJobPackageAsync(src, jobsRoot);
                    }
                    return Results.Ok(new { jobId = newJobId });
                }
                catch (InvalidJobPackageException ipex)
                {
                    AppLogger.Warn($"Invalid job package rejected: {ipex.Message}");
                    return Results.BadRequest(new { error = ipex.Message });
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Failed to import job package", ex);
                    return Results.Problem(detail: ex.Message, statusCode: 500, title: "Import failed");
                }
            }) // (sin DisableAntiforgery: la defensa contra CSRF se hace vía IsTrustedImportClient)
            // Kestrel limita el body a 30 MB y los formularios multipart a
            // 128 MB por defecto; ampliamos al máximo anunciado en la UI.
            .WithMetadata(new RequestSizeLimitAttribute(MaxImportPackageBytes))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxImportPackageBytes });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================");
            Console.WriteLine("  " + Loc.T("Host.ConsoleServerStarted"));
            Console.WriteLine("  " + Loc.T("Host.ConsoleOpenBrowser", LocalBaseUrl));
            Console.WriteLine("  " + Loc.T("Host.ConsoleStopHint"));
            Console.WriteLine("==================================================");
            Console.ResetColor();

            app.Run();
        }

        /// <summary>
        /// Whitelist de valores aceptados en <c>X-Polyglot-Client</c>. Un
        /// atacante cross-origin no puede setear headers custom sin disparar
        /// un preflight CORS que este server no aceptaría.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> TrustedClientHeaders =
            new(System.StringComparer.OrdinalIgnoreCase) { "blazor", "maui", "cli" };

        /// <summary>
        /// Defensa CSRF del endpoint de import: o el request viene de un
        /// navegador same-origin (Origin o Referer contra http://localhost:5000),
        /// o trae un header <c>X-Polyglot-Client</c> whitelisted. Requests
        /// sin Origin (curl, HttpClient local) también pasan, porque el
        /// server sólo escucha en localhost y se asume que un proceso local
        /// ya tiene acceso equivalente al filesystem del usuario.
        /// </summary>
        private static bool IsTrustedImportClient(HttpRequest req)
        {
            // (b) Header custom de cliente confiable
            if (req.Headers.TryGetValue("X-Polyglot-Client", out var clientValues))
            {
                foreach (var v in clientValues)
                {
                    if (!string.IsNullOrEmpty(v) && TrustedClientHeaders.Contains(v))
                    {
                        return true;
                    }
                }
            }

            // (a) Same-origin desde un navegador. Comparamos contra la URL
            // local que UseUrls fijó (http://localhost:5000). Si el servidor
            // se expone detrás de un reverse proxy habría que ajustar esto,
            // pero la app es localhost-only.
            if (req.Headers.TryGetValue("Origin", out var origin) && IsLocalOrigin(origin.ToString()))
            {
                return true;
            }
            if (req.Headers.TryGetValue("Referer", out var referer) && IsLocalOrigin(referer.ToString()))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Compara esquema, host y puerto exactos contra <see cref="LocalBaseUrl"/>.
        /// Un StartsWith aceptaría p.ej. "http://localhost:50001".
        /// </summary>
        private static bool IsLocalOrigin(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            var local = new Uri(LocalBaseUrl);
            return string.Equals(uri.Scheme, local.Scheme, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, local.Host, System.StringComparison.OrdinalIgnoreCase)
                && uri.Port == local.Port;
        }
    }
}
