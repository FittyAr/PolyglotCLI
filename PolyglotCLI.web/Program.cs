using PolyglotCLI.web.Components;
using PolyglotCLI.web.Services;
using Radzen;
using PolyglotCLI;
using Cropper.Blazor.Extensions;

namespace PolyglotCLI.web
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Inicializar la configuracion y el logger
            var config = AppConfig.Load();
            AppLogger.Initialize(config);
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(new ApplicationMode(isWebMode: true));

            // Forzar la URL a localhost:5000
            builder.WebHost.UseUrls("http://localhost:5000");

            // Registrar componentes de Razor y Radzen
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents(options => {
                    options.DetailedErrors = true;
                })
                .AddHubOptions(options => {
                    // Cropper.Blazor envía imágenes base64 sobre SignalR; ampliar el límite por encima del valor por defecto (32KB).
                    // Se amplía a 128MB para soportar payloads grandes (p.ej. miniaturas en base64).
                    options.MaximumReceiveMessageSize = 128 * 1024 * 1024; // 128MB
                });
            builder.Services.AddRadzenComponents();
            builder.Services.AddCropper();

            // HttpClient para que componentes (p.ej. el importador de .zpg) suban archivos directamente
            // al endpoint HTTP sin pasar por SignalR, evitando el límite de tamaño del hub.
            builder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5000")
            });

            // Servicio de empaquetado de trabajos, separado por modo de ejecución
            // (Web usa HTTP endpoints; MAUI usa file pickers nativos).
            builder.Services.AddScoped<IJobPackageHost, WebJobPackageHost>();
            builder.Services.AddScoped<IFolderPickerService, WebFolderPickerService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                app.UseHsts();
            }
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseAntiforgery();
            app.UseStaticFiles();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // -------- Endpoints de exportación / importación de trabajos (.zpg) --------
            // GET /api/jobs/{jobId}/package → descarga el trabajo como .zpg
            app.MapGet("/api/jobs/{jobId}/package", (string jobId) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(jobId) || jobId.Contains("..") || jobId.Contains('/') || jobId.Contains('\\'))
                    {
                        return Results.BadRequest(new { error = "Invalid jobId" });
                    }

                    string jobsRoot = TranslationOrchestrator.GetJobsDirectory();
                    string jobDir = Path.Combine(jobsRoot, jobId);
                    if (!Directory.Exists(jobDir))
                    {
                        return Results.NotFound(new { error = $"Job '{jobId}' not found" });
                    }

                    var stream = new MemoryStream();
                    JobPackageService.ExportJobPackage(jobDir, stream);
                    stream.Position = 0;

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
            app.MapPost("/api/jobs/import", async (HttpRequest req) =>
            {
                try
                {
                    if (!req.HasFormContentType)
                    {
                        return Results.BadRequest(new { error = "Se requiere multipart/form-data" });
                    }

                    var form = await req.ReadFormAsync();
                    var file = form.Files["file"];
                    if (file == null || file.Length == 0)
                    {
                        return Results.BadRequest(new { error = "No se proporcionó archivo" });
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
            }).DisableAntiforgery(); // El cliente Blazor/MAUI envía el multipart directamente

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================");
            Console.WriteLine("  Servidor PolyglotCLI iniciado en Modo Web");
            Console.WriteLine("  Abra su navegador en: http://localhost:5000");
            Console.WriteLine("  Presione Ctrl+C para detener el servidor");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            app.Run();
        }
    }
}
